// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX                        Amd64/x86 Code Generator                           XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/
#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

#ifndef LEGACY_BACKEND // This file is ONLY used for the RyuJIT backend that uses the linear scan register allocator.

#ifdef _TARGET_XARCH_
#include "emit.h"
#include "codegen.h"
#include "lower.h"
#include "gcinfo.h"
#include "gcinfoencoder.h"

/*****************************************************************************
 *
 *  Generate code that will set the given register to the integer constant.
 */

void CodeGen::genSetRegToIcon(regNumber reg, ssize_t val, var_types type, insFlags flags)
{
    // Reg cannot be a FP reg
    assert(!genIsValidFloatReg(reg));

    // The only TYP_REF constant that can come this path is a managed 'null' since it is not
    // relocatable.  Other ref type constants (e.g. string objects) go through a different
    // code path.
    noway_assert(type != TYP_REF || val == 0);

    if (val == 0)
    {
        instGen_Set_Reg_To_Zero(emitActualTypeSize(type), reg, flags);
    }
    else
    {
        // TODO-XArch-CQ: needs all the optimized cases
        getEmitter()->emitIns_R_I(INS_mov, emitActualTypeSize(type), reg, val);
    }
}

/*****************************************************************************
 *
 *   Generate code to check that the GS cookie wasn't thrashed by a buffer
 *   overrun.  If pushReg is true, preserve all registers around code sequence.
 *   Otherwise ECX could be modified.
 *
 *   Implementation Note: pushReg = true, in case of tail calls.
 */
void CodeGen::genEmitGSCookieCheck(bool pushReg)
{
    noway_assert(compiler->gsGlobalSecurityCookieAddr || compiler->gsGlobalSecurityCookieVal);

    // Make sure that EAX is reported as live GC-ref so that any GC that kicks in while
    // executing GS cookie check will not collect the object pointed to by EAX.
    //
    // For Amd64 System V, a two-register-returned struct could be returned in RAX and RDX
    // In such case make sure that the correct GC-ness of RDX is reported as well, so
    // a GC object pointed by RDX will not be collected.
    if (!pushReg)
    {
        // Handle multi-reg return type values
        if (compiler->compMethodReturnsMultiRegRetType())
        {
            ReturnTypeDesc retTypeDesc;
            if (varTypeIsLong(compiler->info.compRetNativeType))
            {
                retTypeDesc.InitializeLongReturnType(compiler);
            }
            else // we must have a struct return type
            {
                retTypeDesc.InitializeStructReturnType(compiler, compiler->info.compMethodInfo->args.retTypeClass);
            }

            unsigned regCount = retTypeDesc.GetReturnRegCount();

            // Only x86 and x64 Unix ABI allows multi-reg return and
            // number of result regs should be equal to MAX_RET_REG_COUNT.
            assert(regCount == MAX_RET_REG_COUNT);

            for (unsigned i = 0; i < regCount; ++i)
            {
                gcInfo.gcMarkRegPtrVal(retTypeDesc.GetABIReturnReg(i), retTypeDesc.GetReturnRegType(i));
            }
        }
        else if (compiler->compMethodReturnsRetBufAddr())
        {
            // This is for returning in an implicit RetBuf.
            // If the address of the buffer is returned in REG_INTRET, mark the content of INTRET as ByRef.

            // In case the return is in an implicit RetBuf, the native return type should be a struct
            assert(varTypeIsStruct(compiler->info.compRetNativeType));

            gcInfo.gcMarkRegPtrVal(REG_INTRET, TYP_BYREF);
        }
        // ... all other cases.
        else
        {
#ifdef _TARGET_AMD64_
            // For x64, structs that are not returned in registers are always
            // returned in implicit RetBuf. If we reached here, we should not have
            // a RetBuf and the return type should not be a struct.
            assert(compiler->info.compRetBuffArg == BAD_VAR_NUM);
            assert(!varTypeIsStruct(compiler->info.compRetNativeType));
#endif // _TARGET_AMD64_

            // For x86 Windows we can't make such assertions since we generate code for returning of
            // the RetBuf in REG_INTRET only when the ProfilerHook is enabled. Otherwise
            // compRetNativeType could be TYP_STRUCT.
            gcInfo.gcMarkRegPtrVal(REG_INTRET, compiler->info.compRetNativeType);
        }
    }

    regNumber regGSCheck;
    regMaskTP regMaskGSCheck = RBM_NONE;

    if (!pushReg)
    {
        // Non-tail call: we can use any callee trash register that is not
        // a return register or contain 'this' pointer (keep alive this), since
        // we are generating GS cookie check after a GT_RETURN block.
        // Note: On Amd64 System V RDX is an arg register - REG_ARG_2 - as well
        // as return register for two-register-returned structs.
        if (compiler->lvaKeepAliveAndReportThis() && compiler->lvaTable[compiler->info.compThisArg].lvRegister &&
            (compiler->lvaTable[compiler->info.compThisArg].lvRegNum == REG_ARG_0))
        {
            regGSCheck = REG_ARG_1;
        }
        else
        {
            regGSCheck = REG_ARG_0;
        }
    }
    else
    {
#ifdef _TARGET_X86_
        // It doesn't matter which register we pick, since we're going to save and restore it
        // around the check.
        // TODO-CQ: Can we optimize the choice of register to avoid doing the push/pop sometimes?
        regGSCheck     = REG_EAX;
        regMaskGSCheck = RBM_EAX;
#else  // !_TARGET_X86_
        // Tail calls from methods that need GS check:  We need to preserve registers while
        // emitting GS cookie check for a tail prefixed call or a jmp. To emit GS cookie
        // check, we might need a register. This won't be an issue for jmp calls for the
        // reason mentioned below (see comment starting with "Jmp Calls:").
        //
        // The following are the possible solutions in case of tail prefixed calls:
        // 1) Use R11 - ignore tail prefix on calls that need to pass a param in R11 when
        //    present in methods that require GS cookie check.  Rest of the tail calls that
        //    do not require R11 will be honored.
        // 2) Internal register - GT_CALL node reserves an internal register and emits GS
        //    cookie check as part of tail call codegen. GenExitCode() needs to special case
        //    fast tail calls implemented as epilog+jmp or such tail calls should always get
        //    dispatched via helper.
        // 3) Materialize GS cookie check as a sperate node hanging off GT_CALL node in
        //    right execution order during rationalization.
        //
        // There are two calls that use R11: VSD and calli pinvokes with cookie param. Tail
        // prefix on pinvokes is ignored.  That is, options 2 and 3 will allow tail prefixed
        // VSD calls from methods that need GS check.
        //
        // Tail prefixed calls: Right now for Jit64 compat, method requiring GS cookie check
        // ignores tail prefix.  In future, if we intend to support tail calls from such a method,
        // consider one of the options mentioned above.  For now adding an assert that we don't
        // expect to see a tail call in a method that requires GS check.
        noway_assert(!compiler->compTailCallUsed);

        // Jmp calls: specify method handle using which JIT queries VM for its entry point
        // address and hence it can neither be a VSD call nor PInvoke calli with cookie
        // parameter.  Therefore, in case of jmp calls it is safe to use R11.
        regGSCheck = REG_R11;
#endif // !_TARGET_X86_
    }

    regMaskTP byrefPushedRegs = RBM_NONE;
    regMaskTP norefPushedRegs = RBM_NONE;
    regMaskTP pushedRegs      = RBM_NONE;

    if (compiler->gsGlobalSecurityCookieAddr == nullptr)
    {
#if defined(_TARGET_AMD64_)
        // If GS cookie value fits within 32-bits we can use 'cmp mem64, imm32'.
        // Otherwise, load the value into a reg and use 'cmp mem64, reg64'.
        if ((int)compiler->gsGlobalSecurityCookieVal != (ssize_t)compiler->gsGlobalSecurityCookieVal)
        {
            genSetRegToIcon(regGSCheck, compiler->gsGlobalSecurityCookieVal, TYP_I_IMPL);
            getEmitter()->emitIns_S_R(INS_cmp, EA_PTRSIZE, regGSCheck, compiler->lvaGSSecurityCookie, 0);
        }
        else
#endif // defined(_TARGET_AMD64_)
        {
            assert((int)compiler->gsGlobalSecurityCookieVal == (ssize_t)compiler->gsGlobalSecurityCookieVal);
            getEmitter()->emitIns_S_I(INS_cmp, EA_PTRSIZE, compiler->lvaGSSecurityCookie, 0,
                                      (int)compiler->gsGlobalSecurityCookieVal);
        }
    }
    else
    {
        // Ngen case - GS cookie value needs to be accessed through an indirection.

        pushedRegs = genPushRegs(regMaskGSCheck, &byrefPushedRegs, &norefPushedRegs);

        instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, regGSCheck, (ssize_t)compiler->gsGlobalSecurityCookieAddr);
        getEmitter()->emitIns_R_AR(ins_Load(TYP_I_IMPL), EA_PTRSIZE, regGSCheck, regGSCheck, 0);
        getEmitter()->emitIns_S_R(INS_cmp, EA_PTRSIZE, regGSCheck, compiler->lvaGSSecurityCookie, 0);
    }

    BasicBlock*  gsCheckBlk = genCreateTempLabel();
    emitJumpKind jmpEqual   = genJumpKindForOper(GT_EQ, CK_SIGNED);
    inst_JMP(jmpEqual, gsCheckBlk);
    genEmitHelperCall(CORINFO_HELP_FAIL_FAST, 0, EA_UNKNOWN);
    genDefineTempLabel(gsCheckBlk);

    genPopRegs(pushedRegs, byrefPushedRegs, norefPushedRegs);
}

BasicBlock* CodeGen::genCallFinally(BasicBlock* block)
{
#if FEATURE_EH_FUNCLETS
    // Generate a call to the finally, like this:
    //      mov         rcx,qword ptr [rbp + 20H]       // Load rcx with PSPSym
    //      call        finally-funclet
    //      jmp         finally-return                  // Only for non-retless finally calls
    // The jmp can be a NOP if we're going to the next block.
    // If we're generating code for the main function (not a funclet), and there is no localloc,
    // then RSP at this point is the same value as that stored in the PSPSym. So just copy RSP
    // instead of loading the PSPSym in this case, or if PSPSym is not used (CoreRT ABI).

    if ((compiler->lvaPSPSym == BAD_VAR_NUM) ||
        (!compiler->compLocallocUsed && (compiler->funCurrentFunc()->funKind == FUNC_ROOT)))
    {
#ifndef UNIX_X86_ABI
        inst_RV_RV(INS_mov, REG_ARG_0, REG_SPBASE, TYP_I_IMPL);
#endif // !UNIX_X86_ABI
    }
    else
    {
        getEmitter()->emitIns_R_S(ins_Load(TYP_I_IMPL), EA_PTRSIZE, REG_ARG_0, compiler->lvaPSPSym, 0);
    }
    getEmitter()->emitIns_J(INS_call, block->bbJumpDest);

    if (block->bbFlags & BBF_RETLESS_CALL)
    {
        // We have a retless call, and the last instruction generated was a call.
        // If the next block is in a different EH region (or is the end of the code
        // block), then we need to generate a breakpoint here (since it will never
        // get executed) to get proper unwind behavior.

        if ((block->bbNext == nullptr) || !BasicBlock::sameEHRegion(block, block->bbNext))
        {
            instGen(INS_BREAKPOINT); // This should never get executed
        }
    }
    else
    {
// TODO-Linux-x86: Do we need to handle the GC information for this NOP or JMP specially, as is done for other
// architectures?
#ifndef JIT32_GCENCODER
        // Because of the way the flowgraph is connected, the liveness info for this one instruction
        // after the call is not (can not be) correct in cases where a variable has a last use in the
        // handler.  So turn off GC reporting for this single instruction.
        getEmitter()->emitDisableGC();
#endif // JIT32_GCENCODER

        // Now go to where the finally funclet needs to return to.
        if (block->bbNext->bbJumpDest == block->bbNext->bbNext)
        {
            // Fall-through.
            // TODO-XArch-CQ: Can we get rid of this instruction, and just have the call return directly
            // to the next instruction? This would depend on stack walking from within the finally
            // handler working without this instruction being in this special EH region.
            instGen(INS_nop);
        }
        else
        {
            inst_JMP(EJ_jmp, block->bbNext->bbJumpDest);
        }

#ifndef JIT32_GCENCODER
        getEmitter()->emitEnableGC();
#endif // JIT32_GCENCODER
    }

#else // !FEATURE_EH_FUNCLETS

    // If we are about to invoke a finally locally from a try block, we have to set the ShadowSP slot
    // corresponding to the finally's nesting level. When invoked in response to an exception, the
    // EE does this.
    //
    // We have a BBJ_CALLFINALLY followed by a BBJ_ALWAYS.
    //
    // We will emit :
    //      mov [ebp - (n + 1)], 0
    //      mov [ebp -  n     ], 0xFC
    //      push &step
    //      jmp  finallyBlock
    // ...
    // step:
    //      mov [ebp -  n     ], 0
    //      jmp leaveTarget
    // ...
    // leaveTarget:

    noway_assert(isFramePointerUsed());

    // Get the nesting level which contains the finally
    unsigned finallyNesting = 0;
    compiler->fgGetNestingLevel(block, &finallyNesting);

    // The last slot is reserved for ICodeManager::FixContext(ppEndRegion)
    unsigned filterEndOffsetSlotOffs;
    filterEndOffsetSlotOffs = (unsigned)(compiler->lvaLclSize(compiler->lvaShadowSPslotsVar) - TARGET_POINTER_SIZE);

    unsigned curNestingSlotOffs;
    curNestingSlotOffs = (unsigned)(filterEndOffsetSlotOffs - ((finallyNesting + 1) * TARGET_POINTER_SIZE));

    // Zero out the slot for the next nesting level
    instGen_Store_Imm_Into_Lcl(TYP_I_IMPL, EA_PTRSIZE, 0, compiler->lvaShadowSPslotsVar,
                               curNestingSlotOffs - TARGET_POINTER_SIZE);
    instGen_Store_Imm_Into_Lcl(TYP_I_IMPL, EA_PTRSIZE, LCL_FINALLY_MARK, compiler->lvaShadowSPslotsVar,
                               curNestingSlotOffs);

    // Now push the address where the finally funclet should return to directly.
    if (!(block->bbFlags & BBF_RETLESS_CALL))
    {
        assert(block->isBBCallAlwaysPair());
        getEmitter()->emitIns_J(INS_push_hide, block->bbNext->bbJumpDest);
    }
    else
    {
        // EE expects a DWORD, so we give him 0
        inst_IV(INS_push_hide, 0);
    }

    // Jump to the finally BB
    inst_JMP(EJ_jmp, block->bbJumpDest);

#endif // !FEATURE_EH_FUNCLETS

    // The BBJ_ALWAYS is used because the BBJ_CALLFINALLY can't point to the
    // jump target using bbJumpDest - that is already used to point
    // to the finally block. So just skip past the BBJ_ALWAYS unless the
    // block is RETLESS.
    if (!(block->bbFlags & BBF_RETLESS_CALL))
    {
        assert(block->isBBCallAlwaysPair());
        block = block->bbNext;
    }
    return block;
}

#if FEATURE_EH_FUNCLETS
void CodeGen::genEHCatchRet(BasicBlock* block)
{
    // Set RAX to the address the VM should return to after the catch.
    // Generate a RIP-relative
    //         lea reg, [rip + disp32] ; the RIP is implicit
    // which will be position-indepenent.
    getEmitter()->emitIns_R_L(INS_lea, EA_PTR_DSP_RELOC, block->bbJumpDest, REG_INTRET);
}

#else // !FEATURE_EH_FUNCLETS

void CodeGen::genEHFinallyOrFilterRet(BasicBlock* block)
{
    // The last statement of the block must be a GT_RETFILT, which has already been generated.
    assert(block->lastNode() != nullptr);
    assert(block->lastNode()->OperGet() == GT_RETFILT);

    if (block->bbJumpKind == BBJ_EHFINALLYRET)
    {
        assert(block->lastNode()->gtOp.gtOp1 == nullptr); // op1 == nullptr means endfinally

        // Return using a pop-jmp sequence. As the "try" block calls
        // the finally with a jmp, this leaves the x86 call-ret stack
        // balanced in the normal flow of path.

        noway_assert(isFramePointerRequired());
        inst_RV(INS_pop_hide, REG_EAX, TYP_I_IMPL);
        inst_RV(INS_i_jmp, REG_EAX, TYP_I_IMPL);
    }
    else
    {
        assert(block->bbJumpKind == BBJ_EHFILTERRET);

        // The return value has already been computed.
        instGen_Return(0);
    }
}

#endif // !FEATURE_EH_FUNCLETS

//  Move an immediate value into an integer register

void CodeGen::instGen_Set_Reg_To_Imm(emitAttr size, regNumber reg, ssize_t imm, insFlags flags)
{
    // reg cannot be a FP register
    assert(!genIsValidFloatReg(reg));

    if (!compiler->opts.compReloc)
    {
        size = EA_SIZE(size); // Strip any Reloc flags from size if we aren't doing relocs
    }

    if ((imm == 0) && !EA_IS_RELOC(size))
    {
        instGen_Set_Reg_To_Zero(size, reg, flags);
    }
    else
    {
        if (genDataIndirAddrCanBeEncodedAsPCRelOffset(imm))
        {
            getEmitter()->emitIns_R_AI(INS_lea, EA_PTR_DSP_RELOC, reg, imm);
        }
        else
        {
            getEmitter()->emitIns_R_I(INS_mov, size, reg, imm);
        }
    }
    regTracker.rsTrackRegIntCns(reg, imm);
}

/***********************************************************************************
 *
 * Generate code to set a register 'targetReg' of type 'targetType' to the constant
 * specified by the constant (GT_CNS_INT or GT_CNS_DBL) in 'tree'. This does not call
 * genProduceReg() on the target register.
 */
void CodeGen::genSetRegToConst(regNumber targetReg, var_types targetType, GenTreePtr tree)
{

    switch (tree->gtOper)
    {
        case GT_CNS_INT:
        {
            // relocatable values tend to come down as a CNS_INT of native int type
            // so the line between these two opcodes is kind of blurry
            GenTreeIntConCommon* con    = tree->AsIntConCommon();
            ssize_t              cnsVal = con->IconValue();

            if (con->ImmedValNeedsReloc(compiler))
            {
                instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, targetReg, cnsVal);
                regTracker.rsTrackRegTrash(targetReg);
            }
            else
            {
                genSetRegToIcon(targetReg, cnsVal, targetType);
            }
        }
        break;

        case GT_CNS_DBL:
        {
            double constValue = tree->gtDblCon.gtDconVal;

            // Make sure we use "xorpd reg, reg"  only for +ve zero constant (0.0) and not for -ve zero (-0.0)
            if (*(__int64*)&constValue == 0)
            {
                // A faster/smaller way to generate 0
                instruction ins = genGetInsForOper(GT_XOR, targetType);
                inst_RV_RV(ins, targetReg, targetReg, targetType);
            }
            else
            {
                GenTreePtr cns;
                if (targetType == TYP_FLOAT)
                {
                    float f = forceCastToFloat(constValue);
                    cns     = genMakeConst(&f, targetType, tree, false);
                }
                else
                {
                    cns = genMakeConst(&constValue, targetType, tree, true);
                }

                inst_RV_TT(ins_Load(targetType), targetReg, cns);
            }
        }
        break;

        default:
            unreached();
    }
}

// Generate code to get the high N bits of a N*N=2N bit multiplication result
void CodeGen::genCodeForMulHi(GenTreeOp* treeNode)
{
    if (treeNode->OperGet() == GT_MULHI)
    {
        assert(!(treeNode->gtFlags & GTF_UNSIGNED));
    }
    assert(!treeNode->gtOverflowEx());

    regNumber targetReg  = treeNode->gtRegNum;
    var_types targetType = treeNode->TypeGet();
    emitter*  emit       = getEmitter();
    emitAttr  size       = emitTypeSize(treeNode);
    GenTree*  op1        = treeNode->gtOp.gtOp1;
    GenTree*  op2        = treeNode->gtOp.gtOp2;

    // to get the high bits of the multiply, we are constrained to using the
    // 1-op form:  RDX:RAX = RAX * rm
    // The 3-op form (Rx=Ry*Rz) does not support it.

    genConsumeOperands(treeNode->AsOp());

    GenTree* regOp = op1;
    GenTree* rmOp  = op2;

    // Set rmOp to the memory operand (if any)
    if (op1->isUsedFromMemory() || (op2->isUsedFromReg() && (op2->gtRegNum == REG_RAX)))
    {
        regOp = op2;
        rmOp  = op1;
    }
    assert(regOp->isUsedFromReg());

    // Setup targetReg when neither of the source operands was a matching register
    if (regOp->gtRegNum != REG_RAX)
    {
        inst_RV_RV(ins_Copy(targetType), REG_RAX, regOp->gtRegNum, targetType);
    }

    instruction ins;
    if ((treeNode->gtFlags & GTF_UNSIGNED) == 0)
    {
        ins = INS_imulEAX;
    }
    else
    {
        ins = INS_mulEAX;
    }
    emit->emitInsBinary(ins, size, treeNode, rmOp);

    // Move the result to the desired register, if necessary
    if (treeNode->OperGet() == GT_MULHI && targetReg != REG_RDX)
    {
        inst_RV_RV(INS_mov, targetReg, REG_RDX, targetType);
    }
}

#ifdef _TARGET_X86_
//------------------------------------------------------------------------
// genCodeForLongUMod: Generate code for a tree of the form
//                     `(umod (gt_long x y) (const int))`
//
// Arguments:
//   node - the node for which to generate code
//
void CodeGen::genCodeForLongUMod(GenTreeOp* node)
{
    assert(node != nullptr);
    assert(node->OperGet() == GT_UMOD);
    assert(node->TypeGet() == TYP_INT);

    GenTreeOp* const dividend = node->gtOp1->AsOp();
    assert(dividend->OperGet() == GT_LONG);
    assert(varTypeIsLong(dividend));

    genConsumeOperands(node);

    GenTree* const dividendLo = dividend->gtOp1;
    GenTree* const dividendHi = dividend->gtOp2;
    assert(dividendLo->isUsedFromReg());
    assert(dividendHi->isUsedFromReg());

    GenTree* const divisor = node->gtOp2;
    assert(divisor->gtSkipReloadOrCopy()->OperGet() == GT_CNS_INT);
    assert(divisor->gtSkipReloadOrCopy()->isUsedFromReg());
    assert(divisor->gtSkipReloadOrCopy()->AsIntCon()->gtIconVal >= 2);
    assert(divisor->gtSkipReloadOrCopy()->AsIntCon()->gtIconVal <= 0x3fffffff);

    // dividendLo must be in RAX; dividendHi must be in RDX
    genCopyRegIfNeeded(dividendLo, REG_EAX);
    genCopyRegIfNeeded(dividendHi, REG_EDX);

    // At this point, EAX:EDX contains the 64bit dividend and op2->gtRegNum
    // contains the 32bit divisor. We want to generate the following code:
    //
    //   cmp edx, divisor->gtRegNum
    //   jb noOverflow
    //
    //   mov temp, eax
    //   mov eax, edx
    //   xor edx, edx
    //   div divisor->gtRegNum
    //   mov eax, temp
    //
    // noOverflow:
    //   div divisor->gtRegNum
    //
    // This works because (a * 2^32 + b) % c = ((a % c) * 2^32 + b) % c.

    BasicBlock* const noOverflow = genCreateTempLabel();

    //   cmp edx, divisor->gtRegNum
    //   jb noOverflow
    inst_RV_RV(INS_cmp, REG_EDX, divisor->gtRegNum);
    inst_JMP(EJ_jb, noOverflow);

    //   mov temp, eax
    //   mov eax, edx
    //   xor edx, edx
    //   div divisor->gtRegNum
    //   mov eax, temp
    const regNumber tempReg = node->GetSingleTempReg();
    inst_RV_RV(INS_mov, tempReg, REG_EAX, TYP_INT);
    inst_RV_RV(INS_mov, REG_EAX, REG_EDX, TYP_INT);
    instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_EDX);
    inst_RV(INS_div, divisor->gtRegNum, TYP_INT);
    inst_RV_RV(INS_mov, REG_EAX, tempReg, TYP_INT);

    // noOverflow:
    //   div divisor->gtRegNum
    genDefineTempLabel(noOverflow);
    inst_RV(INS_div, divisor->gtRegNum, TYP_INT);

    const regNumber targetReg = node->gtRegNum;
    if (targetReg != REG_EDX)
    {
        inst_RV_RV(INS_mov, targetReg, REG_RDX, TYP_INT);
    }
    genProduceReg(node);
}
#endif // _TARGET_X86_

//------------------------------------------------------------------------
// genCodeForDivMod: Generate code for a DIV or MOD operation.
//
// Arguments:
//    treeNode - the node to generate the code for
//
void CodeGen::genCodeForDivMod(GenTreeOp* treeNode)
{
    GenTree* dividend = treeNode->gtOp1;
#ifdef _TARGET_X86_
    if (varTypeIsLong(dividend->TypeGet()))
    {
        genCodeForLongUMod(treeNode);
        return;
    }
#endif // _TARGET_X86_

    GenTree*   divisor    = treeNode->gtOp2;
    genTreeOps oper       = treeNode->OperGet();
    emitAttr   size       = emitTypeSize(treeNode);
    regNumber  targetReg  = treeNode->gtRegNum;
    var_types  targetType = treeNode->TypeGet();
    emitter*   emit       = getEmitter();

    // dividend is in a register.
    assert(dividend->isUsedFromReg());

    genConsumeOperands(treeNode->AsOp());
    if (varTypeIsFloating(targetType))
    {
        // Check that divisor is a valid operand.
        // Note that a reg optional operand is a treated as a memory op
        // if no register is allocated to it.
        assert(divisor->isUsedFromReg() || divisor->isMemoryOp() || divisor->IsCnsFltOrDbl() ||
               divisor->IsRegOptional());

        // Floating point div/rem operation
        assert(oper == GT_DIV || oper == GT_MOD);

        if (dividend->gtRegNum == targetReg)
        {
            emit->emitInsBinary(genGetInsForOper(treeNode->gtOper, targetType), size, treeNode, divisor);
        }
        else if (divisor->isUsedFromReg() && divisor->gtRegNum == targetReg)
        {
            // It is not possible to generate 2-operand divss or divsd where reg2 = reg1 / reg2
            // because divss/divsd reg1, reg2 will over-write reg1.  Therefore, in case of AMD64
            // LSRA has to make sure that such a register assignment is not generated for floating
            // point div/rem operations.
            noway_assert(
                !"GT_DIV/GT_MOD (float): case of reg2 = reg1 / reg2, LSRA should never generate such a reg assignment");
        }
        else
        {
            inst_RV_RV(ins_Copy(targetType), targetReg, dividend->gtRegNum, targetType);
            emit->emitInsBinary(genGetInsForOper(treeNode->gtOper, targetType), size, treeNode, divisor);
        }
    }
    else
    {
        // dividend must be in RAX
        genCopyRegIfNeeded(dividend, REG_RAX);

        // zero or sign extend rax to rdx
        if (oper == GT_UMOD || oper == GT_UDIV)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_EDX);
        }
        else
        {
            emit->emitIns(INS_cdq, size);
            // the cdq instruction writes RDX, So clear the gcInfo for RDX
            gcInfo.gcMarkRegSetNpt(RBM_RDX);
        }

        // Perform the 'targetType' (64-bit or 32-bit) divide instruction
        instruction ins;
        if (oper == GT_UMOD || oper == GT_UDIV)
        {
            ins = INS_div;
        }
        else
        {
            ins = INS_idiv;
        }

        emit->emitInsBinary(ins, size, treeNode, divisor);

        // DIV/IDIV instructions always store the quotient in RAX and the remainder in RDX.
        // Move the result to the desired register, if necessary
        if (oper == GT_DIV || oper == GT_UDIV)
        {
            if (targetReg != REG_RAX)
            {
                inst_RV_RV(INS_mov, targetReg, REG_RAX, targetType);
            }
        }
        else
        {
            assert((oper == GT_MOD) || (oper == GT_UMOD));
            if (targetReg != REG_RDX)
            {
                inst_RV_RV(INS_mov, targetReg, REG_RDX, targetType);
            }
        }
    }
    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genCodeForBinary: Generate code for many binary arithmetic operators
// This method is expected to have called genConsumeOperands() before calling it.
//
// Arguments:
//    treeNode - The binary operation for which we are generating code.
//
// Return Value:
//    None.
//
// Notes:
//    Mul and div variants have special constraints on x64 so are not handled here.
//    See teh assert below for the operators that are handled.

void CodeGen::genCodeForBinary(GenTree* treeNode)
{
    const genTreeOps oper       = treeNode->OperGet();
    regNumber        targetReg  = treeNode->gtRegNum;
    var_types        targetType = treeNode->TypeGet();
    emitter*         emit       = getEmitter();

#if defined(_TARGET_64BIT_)
    assert(oper == GT_OR || oper == GT_XOR || oper == GT_AND || oper == GT_ADD || oper == GT_SUB);
#else  // !defined(_TARGET_64BIT_)
    assert(oper == GT_OR || oper == GT_XOR || oper == GT_AND || oper == GT_ADD_LO || oper == GT_ADD_HI ||
           oper == GT_SUB_LO || oper == GT_SUB_HI || oper == GT_MUL_LONG || oper == GT_DIV_HI || oper == GT_MOD_HI ||
           oper == GT_ADD || oper == GT_SUB);
#endif // !defined(_TARGET_64BIT_)

    GenTreePtr op1 = treeNode->gtGetOp1();
    GenTreePtr op2 = treeNode->gtGetOp2();

    // Commutative operations can mark op1 as contained or reg-optional to generate "op reg, memop/immed"
    if (!op1->isUsedFromReg())
    {
        assert(treeNode->OperIsCommutative());
        assert(op1->isMemoryOp() || op1->IsCnsNonZeroFltOrDbl() || op1->IsIntCnsFitsInI32() || op1->IsRegOptional());

        op1 = treeNode->gtGetOp2();
        op2 = treeNode->gtGetOp1();
    }

    instruction ins = genGetInsForOper(treeNode->OperGet(), targetType);

    // The arithmetic node must be sitting in a register (since it's not contained)
    noway_assert(targetReg != REG_NA);

    regNumber op1reg = op1->isUsedFromReg() ? op1->gtRegNum : REG_NA;
    regNumber op2reg = op2->isUsedFromReg() ? op2->gtRegNum : REG_NA;

    GenTreePtr dst;
    GenTreePtr src;

    // This is the case of reg1 = reg1 op reg2
    // We're ready to emit the instruction without any moves
    if (op1reg == targetReg)
    {
        dst = op1;
        src = op2;
    }
    // We have reg1 = reg2 op reg1
    // In order for this operation to be correct
    // we need that op is a commutative operation so
    // we can convert it into reg1 = reg1 op reg2 and emit
    // the same code as above
    else if (op2reg == targetReg)
    {
        noway_assert(GenTree::OperIsCommutative(oper));
        dst = op2;
        src = op1;
    }
    // now we know there are 3 different operands so attempt to use LEA
    else if (oper == GT_ADD && !varTypeIsFloating(treeNode) && !treeNode->gtOverflowEx() // LEA does not set flags
             && (op2->isContainedIntOrIImmed() || op2->isUsedFromReg()) && !treeNode->gtSetFlags())
    {
        if (op2->isContainedIntOrIImmed())
        {
            emit->emitIns_R_AR(INS_lea, emitTypeSize(treeNode), targetReg, op1reg,
                               (int)op2->AsIntConCommon()->IconValue());
        }
        else
        {
            assert(op2reg != REG_NA);
            emit->emitIns_R_ARX(INS_lea, emitTypeSize(treeNode), targetReg, op1reg, op2reg, 1, 0);
        }
        genProduceReg(treeNode);
        return;
    }
    // dest, op1 and op2 registers are different:
    // reg3 = reg1 op reg2
    // We can implement this by issuing a mov:
    // reg3 = reg1
    // reg3 = reg3 op reg2
    else
    {
        inst_RV_RV(ins_Copy(targetType), targetReg, op1reg, targetType);
        regTracker.rsTrackRegCopy(targetReg, op1reg);
        gcInfo.gcMarkRegPtrVal(targetReg, targetType);
        dst = treeNode;
        src = op2;
    }

    // try to use an inc or dec
    if (oper == GT_ADD && !varTypeIsFloating(treeNode) && src->isContainedIntOrIImmed() && !treeNode->gtOverflowEx())
    {
        if (src->IsIntegralConst(1))
        {
            emit->emitIns_R(INS_inc, emitTypeSize(treeNode), targetReg);
            genProduceReg(treeNode);
            return;
        }
        else if (src->IsIntegralConst(-1))
        {
            emit->emitIns_R(INS_dec, emitTypeSize(treeNode), targetReg);
            genProduceReg(treeNode);
            return;
        }
    }
    regNumber r = emit->emitInsBinary(ins, emitTypeSize(treeNode), dst, src);
    noway_assert(r == targetReg);

    if (treeNode->gtOverflowEx())
    {
#if !defined(_TARGET_64BIT_)
        assert(oper == GT_ADD || oper == GT_SUB || oper == GT_ADD_HI || oper == GT_SUB_HI);
#else
        assert(oper == GT_ADD || oper == GT_SUB);
#endif
        genCheckOverflow(treeNode);
    }
    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// isStructReturn: Returns whether the 'treeNode' is returning a struct.
//
// Arguments:
//    treeNode - The tree node to evaluate whether is a struct return.
//
// Return Value:
//    For AMD64 *nix: returns true if the 'treeNode" is a GT_RETURN node, of type struct.
//                    Otherwise returns false.
//    For other platforms always returns false.
//
bool CodeGen::isStructReturn(GenTreePtr treeNode)
{
    // This method could be called for 'treeNode' of GT_RET_FILT or GT_RETURN.
    // For the GT_RET_FILT, the return is always
    // a bool or a void, for the end of a finally block.
    noway_assert(treeNode->OperGet() == GT_RETURN || treeNode->OperGet() == GT_RETFILT);
    if (treeNode->OperGet() != GT_RETURN)
    {
        return false;
    }

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING
    return varTypeIsStruct(treeNode);
#else  // !FEATURE_UNIX_AMD64_STRUCT_PASSING
    assert(!varTypeIsStruct(treeNode));
    return false;
#endif // FEATURE_UNIX_AMD64_STRUCT_PASSING
}

//------------------------------------------------------------------------
// genStructReturn: Generates code for returning a struct.
//
// Arguments:
//    treeNode - The GT_RETURN tree node.
//
// Return Value:
//    None
//
// Assumption:
//    op1 of GT_RETURN node is either GT_LCL_VAR or multi-reg GT_CALL
void CodeGen::genStructReturn(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_RETURN);
    GenTreePtr op1 = treeNode->gtGetOp1();

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING
    if (op1->OperGet() == GT_LCL_VAR)
    {
        GenTreeLclVarCommon* lclVar = op1->AsLclVarCommon();
        LclVarDsc*           varDsc = &(compiler->lvaTable[lclVar->gtLclNum]);
        assert(varDsc->lvIsMultiRegRet);

        ReturnTypeDesc retTypeDesc;
        retTypeDesc.InitializeStructReturnType(compiler, varDsc->lvVerTypeInfo.GetClassHandle());
        unsigned regCount = retTypeDesc.GetReturnRegCount();
        assert(regCount == MAX_RET_REG_COUNT);

        if (varTypeIsEnregisterableStruct(op1))
        {
            // Right now the only enregistrable structs supported are SIMD vector types.
            assert(varTypeIsSIMD(op1));
            assert(op1->isUsedFromReg());

            // This is a case of operand is in a single reg and needs to be
            // returned in multiple ABI return registers.
            regNumber opReg = genConsumeReg(op1);
            regNumber reg0  = retTypeDesc.GetABIReturnReg(0);
            regNumber reg1  = retTypeDesc.GetABIReturnReg(1);

            if (opReg != reg0 && opReg != reg1)
            {
                // Operand reg is different from return regs.
                // Copy opReg to reg0 and let it to be handled by one of the
                // two cases below.
                inst_RV_RV(ins_Copy(TYP_DOUBLE), reg0, opReg, TYP_DOUBLE);
                opReg = reg0;
            }

            if (opReg == reg0)
            {
                assert(opReg != reg1);

                // reg0 - already has required 8-byte in bit position [63:0].
                // reg1 = opReg.
                // swap upper and lower 8-bytes of reg1 so that desired 8-byte is in bit position [63:0].
                inst_RV_RV(ins_Copy(TYP_DOUBLE), reg1, opReg, TYP_DOUBLE);
            }
            else
            {
                assert(opReg == reg1);

                // reg0 = opReg.
                // swap upper and lower 8-bytes of reg1 so that desired 8-byte is in bit position [63:0].
                inst_RV_RV(ins_Copy(TYP_DOUBLE), reg0, opReg, TYP_DOUBLE);
            }
            inst_RV_RV_IV(INS_shufpd, EA_16BYTE, reg1, reg1, 0x01);
        }
        else
        {
            assert(op1->isUsedFromMemory());

            // Copy var on stack into ABI return registers
            int offset = 0;
            for (unsigned i = 0; i < regCount; ++i)
            {
                var_types type = retTypeDesc.GetReturnRegType(i);
                regNumber reg  = retTypeDesc.GetABIReturnReg(i);
                getEmitter()->emitIns_R_S(ins_Load(type), emitTypeSize(type), reg, lclVar->gtLclNum, offset);
                offset += genTypeSize(type);
            }
        }
    }
    else
    {
        assert(op1->IsMultiRegCall() || op1->IsCopyOrReloadOfMultiRegCall());

        genConsumeRegs(op1);

        GenTree*        actualOp1   = op1->gtSkipReloadOrCopy();
        GenTreeCall*    call        = actualOp1->AsCall();
        ReturnTypeDesc* retTypeDesc = call->GetReturnTypeDesc();
        unsigned        regCount    = retTypeDesc->GetReturnRegCount();
        assert(regCount == MAX_RET_REG_COUNT);

        // Handle circular dependency between call allocated regs and ABI return regs.
        //
        // It is possible under LSRA stress that originally allocated regs of call node,
        // say rax and rdx, are spilled and reloaded to rdx and rax respectively.  But
        // GT_RETURN needs to  move values as follows: rdx->rax, rax->rdx. Similar kind
        // kind of circular dependency could arise between xmm0 and xmm1 return regs.
        // Codegen is expected to handle such circular dependency.
        //
        var_types regType0      = retTypeDesc->GetReturnRegType(0);
        regNumber returnReg0    = retTypeDesc->GetABIReturnReg(0);
        regNumber allocatedReg0 = call->GetRegNumByIdx(0);

        var_types regType1      = retTypeDesc->GetReturnRegType(1);
        regNumber returnReg1    = retTypeDesc->GetABIReturnReg(1);
        regNumber allocatedReg1 = call->GetRegNumByIdx(1);

        if (op1->IsCopyOrReload())
        {
            // GT_COPY/GT_RELOAD will have valid reg for those positions
            // that need to be copied or reloaded.
            regNumber reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(0);
            if (reloadReg != REG_NA)
            {
                allocatedReg0 = reloadReg;
            }

            reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(1);
            if (reloadReg != REG_NA)
            {
                allocatedReg1 = reloadReg;
            }
        }

        if (allocatedReg0 == returnReg1 && allocatedReg1 == returnReg0)
        {
            // Circular dependency - swap allocatedReg0 and allocatedReg1
            if (varTypeIsFloating(regType0))
            {
                assert(varTypeIsFloating(regType1));

                // The fastest way to swap two XMM regs is using PXOR
                inst_RV_RV(INS_pxor, allocatedReg0, allocatedReg1, TYP_DOUBLE);
                inst_RV_RV(INS_pxor, allocatedReg1, allocatedReg0, TYP_DOUBLE);
                inst_RV_RV(INS_pxor, allocatedReg0, allocatedReg1, TYP_DOUBLE);
            }
            else
            {
                assert(varTypeIsIntegral(regType0));
                assert(varTypeIsIntegral(regType1));
                inst_RV_RV(INS_xchg, allocatedReg1, allocatedReg0, TYP_I_IMPL);
            }
        }
        else if (allocatedReg1 == returnReg0)
        {
            // Change the order of moves to correctly handle dependency.
            if (allocatedReg1 != returnReg1)
            {
                inst_RV_RV(ins_Copy(regType1), returnReg1, allocatedReg1, regType1);
            }

            if (allocatedReg0 != returnReg0)
            {
                inst_RV_RV(ins_Copy(regType0), returnReg0, allocatedReg0, regType0);
            }
        }
        else
        {
            // No circular dependency case.
            if (allocatedReg0 != returnReg0)
            {
                inst_RV_RV(ins_Copy(regType0), returnReg0, allocatedReg0, regType0);
            }

            if (allocatedReg1 != returnReg1)
            {
                inst_RV_RV(ins_Copy(regType1), returnReg1, allocatedReg1, regType1);
            }
        }
    }
#else
    unreached();
#endif
}

//------------------------------------------------------------------------
// genReturn: Generates code for return statement.
//            In case of struct return, delegates to the genStructReturn method.
//
// Arguments:
//    treeNode - The GT_RETURN or GT_RETFILT tree node.
//
// Return Value:
//    None
//
void CodeGen::genReturn(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_RETURN || treeNode->OperGet() == GT_RETFILT);
    GenTreePtr op1        = treeNode->gtGetOp1();
    var_types  targetType = treeNode->TypeGet();

#ifdef DEBUG
    if (targetType == TYP_VOID)
    {
        assert(op1 == nullptr);
    }
#endif

#ifdef _TARGET_X86_
    if (treeNode->TypeGet() == TYP_LONG)
    {
        assert(op1 != nullptr);
        noway_assert(op1->OperGet() == GT_LONG);
        GenTree* loRetVal = op1->gtGetOp1();
        GenTree* hiRetVal = op1->gtGetOp2();
        noway_assert((loRetVal->gtRegNum != REG_NA) && (hiRetVal->gtRegNum != REG_NA));

        genConsumeReg(loRetVal);
        genConsumeReg(hiRetVal);
        if (loRetVal->gtRegNum != REG_LNGRET_LO)
        {
            inst_RV_RV(ins_Copy(targetType), REG_LNGRET_LO, loRetVal->gtRegNum, TYP_INT);
        }
        if (hiRetVal->gtRegNum != REG_LNGRET_HI)
        {
            inst_RV_RV(ins_Copy(targetType), REG_LNGRET_HI, hiRetVal->gtRegNum, TYP_INT);
        }
    }
    else
#endif // !defined(_TARGET_X86_)
    {
        if (isStructReturn(treeNode))
        {
            genStructReturn(treeNode);
        }
        else if (targetType != TYP_VOID)
        {
            assert(op1 != nullptr);
            noway_assert(op1->gtRegNum != REG_NA);

            // !! NOTE !! genConsumeReg will clear op1 as GC ref after it has
            // consumed a reg for the operand. This is because the variable
            // is dead after return. But we are issuing more instructions
            // like "profiler leave callback" after this consumption. So
            // if you are issuing more instructions after this point,
            // remember to keep the variable live up until the new method
            // exit point where it is actually dead.
            genConsumeReg(op1);

            regNumber retReg = varTypeIsFloating(treeNode) ? REG_FLOATRET : REG_INTRET;
#ifdef _TARGET_X86_
            if (varTypeIsFloating(treeNode))
            {
                // Spill the return value register from an XMM register to the stack, then load it on the x87 stack.
                // If it already has a home location, use that. Otherwise, we need a temp.
                if (genIsRegCandidateLocal(op1) && compiler->lvaTable[op1->gtLclVarCommon.gtLclNum].lvOnFrame)
                {
                    // Store local variable to its home location, if necessary.
                    if ((op1->gtFlags & GTF_REG_VAL) != 0)
                    {
                        op1->gtFlags &= ~GTF_REG_VAL;
                        inst_TT_RV(ins_Store(op1->gtType,
                                             compiler->isSIMDTypeLocalAligned(op1->gtLclVarCommon.gtLclNum)),
                                   op1, op1->gtRegNum);
                    }
                    // Now, load it to the fp stack.
                    getEmitter()->emitIns_S(INS_fld, emitTypeSize(op1), op1->AsLclVarCommon()->gtLclNum, 0);
                }
                else
                {
                    // Spill the value, which should be in a register, then load it to the fp stack.
                    // TODO-X86-CQ: Deal with things that are already in memory (don't call genConsumeReg yet).
                    op1->gtFlags |= GTF_SPILL;
                    regSet.rsSpillTree(op1->gtRegNum, op1);
                    op1->gtFlags |= GTF_SPILLED;
                    op1->gtFlags &= ~GTF_SPILL;

                    TempDsc* t = regSet.rsUnspillInPlace(op1, op1->gtRegNum);
                    inst_FS_ST(INS_fld, emitActualTypeSize(op1->gtType), t, 0);
                    op1->gtFlags &= ~GTF_SPILLED;
                    compiler->tmpRlsTemp(t);
                }
            }
            else
#endif // _TARGET_X86_
            {
                if (op1->gtRegNum != retReg)
                {
                    inst_RV_RV(ins_Copy(targetType), retReg, op1->gtRegNum, targetType);
                }
            }
        }
    }

#ifdef PROFILING_SUPPORTED
    // !! Note !!
    // TODO-AMD64-Unix: If the profiler hook is implemented on *nix, make sure for 2 register returned structs
    //                  the RAX and RDX needs to be kept alive. Make the necessary changes in lowerxarch.cpp
    //                  in the handling of the GT_RETURN statement.
    //                  Such structs containing GC pointers need to be handled by calling gcInfo.gcMarkRegSetNpt
    //                  for the return registers containing GC refs.

    // There will be a single return block while generating profiler ELT callbacks.
    //
    // Reason for not materializing Leave callback as a GT_PROF_HOOK node after GT_RETURN:
    // In flowgraph and other places assert that the last node of a block marked as
    // BBJ_RETURN is either a GT_RETURN or GT_JMP or a tail call.  It would be nice to
    // maintain such an invariant irrespective of whether profiler hook needed or not.
    // Also, there is not much to be gained by materializing it as an explicit node.
    if (compiler->compCurBB == compiler->genReturnBB)
    {
        // !! NOTE !!
        // Since we are invalidating the assumption that we would slip into the epilog
        // right after the "return", we need to preserve the return reg's GC state
        // across the call until actual method return.
        if (varTypeIsGC(compiler->info.compRetType))
        {
            gcInfo.gcMarkRegPtrVal(REG_INTRET, compiler->info.compRetType);
        }

        genProfilingLeaveCallback();

        if (varTypeIsGC(compiler->info.compRetType))
        {
            gcInfo.gcMarkRegSetNpt(REG_INTRET);
        }
    }
#endif
}

/*****************************************************************************
 *
 * Generate code for a single node in the tree.
 * Preconditions: All operands have been evaluated
 *
 */
void CodeGen::genCodeForTreeNode(GenTreePtr treeNode)
{
    regNumber targetReg;
#if !defined(_TARGET_64BIT_)
    if (treeNode->TypeGet() == TYP_LONG)
    {
        // All long enregistered nodes will have been decomposed into their
        // constituent lo and hi nodes.
        targetReg = REG_NA;
    }
    else
#endif // !defined(_TARGET_64BIT_)
    {
        targetReg = treeNode->gtRegNum;
    }
    var_types targetType = treeNode->TypeGet();
    emitter*  emit       = getEmitter();

#ifdef DEBUG
    // Validate that all the operands for the current node are consumed in order.
    // This is important because LSRA ensures that any necessary copies will be
    // handled correctly.
    lastConsumedNode = nullptr;
    if (compiler->verbose)
    {
        unsigned seqNum = treeNode->gtSeqNum; // Useful for setting a conditional break in Visual Studio
        compiler->gtDispLIRNode(treeNode, "Generating: ");
    }
#endif // DEBUG

    // Is this a node whose value is already in a register?  LSRA denotes this by
    // setting the GTF_REUSE_REG_VAL flag.
    if (treeNode->IsReuseRegVal())
    {
        // For now, this is only used for constant nodes.
        assert((treeNode->OperIsConst()));
        JITDUMP("  TreeNode is marked ReuseReg\n");
        return;
    }

    // contained nodes are part of their parents for codegen purposes
    // ex : immediates, most LEAs
    if (treeNode->isContained())
    {
        return;
    }

    switch (treeNode->gtOper)
    {
#ifndef JIT32_GCENCODER
        case GT_START_NONGC:
            getEmitter()->emitDisableGC();
            break;
#endif // !defined(JIT32_GCENCODER)

        case GT_PROF_HOOK:
#ifdef PROFILING_SUPPORTED
            // We should be seeing this only if profiler hook is needed
            noway_assert(compiler->compIsProfilerHookNeeded());

            // Right now this node is used only for tail calls. In future if
            // we intend to use it for Enter or Leave hooks, add a data member
            // to this node indicating the kind of profiler hook. For example,
            // helper number can be used.
            genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
#endif // PROFILING_SUPPORTED
            break;

        case GT_LCLHEAP:
            genLclHeap(treeNode);
            break;

        case GT_CNS_INT:
#ifdef _TARGET_X86_
            assert(!treeNode->IsIconHandle(GTF_ICON_TLS_HDL));
#endif // _TARGET_X86_
            __fallthrough;

        case GT_CNS_DBL:
            genSetRegToConst(targetReg, targetType, treeNode);
            genProduceReg(treeNode);
            break;

        case GT_NEG:
        case GT_NOT:
            if (varTypeIsFloating(targetType))
            {
                assert(treeNode->gtOper == GT_NEG);
                genSSE2BitwiseOp(treeNode);
            }
            else
            {
                GenTreePtr operand = treeNode->gtGetOp1();
                assert(operand->isUsedFromReg());
                regNumber operandReg = genConsumeReg(operand);

                if (operandReg != targetReg)
                {
                    inst_RV_RV(INS_mov, targetReg, operandReg, targetType);
                }

                instruction ins = genGetInsForOper(treeNode->OperGet(), targetType);
                inst_RV(ins, targetReg, targetType);
            }
            genProduceReg(treeNode);
            break;

        case GT_OR:
        case GT_XOR:
        case GT_AND:
            assert(varTypeIsIntegralOrI(treeNode));
            __fallthrough;

#if !defined(_TARGET_64BIT_)
        case GT_ADD_LO:
        case GT_ADD_HI:
        case GT_SUB_LO:
        case GT_SUB_HI:
#endif // !defined(_TARGET_64BIT_)
        case GT_ADD:
        case GT_SUB:
            genConsumeOperands(treeNode->AsOp());
            genCodeForBinary(treeNode);
            break;

        case GT_LSH:
        case GT_RSH:
        case GT_RSZ:
        case GT_ROL:
        case GT_ROR:
            genCodeForShift(treeNode);
            // genCodeForShift() calls genProduceReg()
            break;

#if !defined(_TARGET_64BIT_)
        case GT_LSH_HI:
        case GT_RSH_LO:
            // TODO-X86-CQ: This only handles the case where the operand being shifted is in a register. We don't
            // need sourceHi to be always in reg in case of GT_LSH_HI (because it could be moved from memory to
            // targetReg if sourceHi is a memory operand). Similarly for GT_RSH_LO, sourceLo could be marked as
            // contained memory-op. Even if not a memory-op, we could mark it as reg-optional.
            genCodeForShiftLong(treeNode);
            break;
#endif

        case GT_CAST:
            if (varTypeIsFloating(targetType) && varTypeIsFloating(treeNode->gtOp.gtOp1))
            {
                // Casts float/double <--> double/float
                genFloatToFloatCast(treeNode);
            }
            else if (varTypeIsFloating(treeNode->gtOp.gtOp1))
            {
                // Casts float/double --> int32/int64
                genFloatToIntCast(treeNode);
            }
            else if (varTypeIsFloating(targetType))
            {
                // Casts int32/uint32/int64/uint64 --> float/double
                genIntToFloatCast(treeNode);
            }
            else
            {
                // Casts int <--> int
                genIntToIntCast(treeNode);
            }
            // The per-case functions call genProduceReg()
            break;

        case GT_LCL_VAR:
        {
            // lcl_vars are not defs
            assert((treeNode->gtFlags & GTF_VAR_DEF) == 0);

            GenTreeLclVarCommon* lcl            = treeNode->AsLclVarCommon();
            bool                 isRegCandidate = compiler->lvaTable[lcl->gtLclNum].lvIsRegCandidate();

            if (isRegCandidate && !(treeNode->gtFlags & GTF_VAR_DEATH))
            {
                assert(treeNode->InReg() || (treeNode->gtFlags & GTF_SPILLED));
            }

            // If this is a register candidate that has been spilled, genConsumeReg() will
            // reload it at the point of use.  Otherwise, if it's not in a register, we load it here.

            if (!treeNode->InReg() && !(treeNode->gtFlags & GTF_SPILLED))
            {
                assert(!isRegCandidate);
#if defined(FEATURE_SIMD) && defined(_TARGET_X86_)
                // Loading of TYP_SIMD12 (i.e. Vector3) variable
                if (treeNode->TypeGet() == TYP_SIMD12)
                {
                    genLoadLclTypeSIMD12(treeNode);
                    break;
                }
#endif // defined(FEATURE_SIMD) && defined(_TARGET_X86_)

                emit->emitIns_R_S(ins_Load(treeNode->TypeGet(), compiler->isSIMDTypeLocalAligned(lcl->gtLclNum)),
                                  emitTypeSize(treeNode), treeNode->gtRegNum, lcl->gtLclNum, 0);
                genProduceReg(treeNode);
            }
        }
        break;

        case GT_LCL_FLD_ADDR:
        case GT_LCL_VAR_ADDR:
            // Address of a local var.  This by itself should never be allocated a register.
            // If it is worth storing the address in a register then it should be cse'ed into
            // a temp and that would be allocated a register.
            noway_assert(targetType == TYP_BYREF);
            noway_assert(!treeNode->InReg());

            inst_RV_TT(INS_lea, targetReg, treeNode, 0, EA_BYREF);
            genProduceReg(treeNode);
            break;

        case GT_LCL_FLD:
        {
            noway_assert(targetType != TYP_STRUCT);
            noway_assert(treeNode->gtRegNum != REG_NA);

#ifdef FEATURE_SIMD
            // Loading of TYP_SIMD12 (i.e. Vector3) field
            if (treeNode->TypeGet() == TYP_SIMD12)
            {
                genLoadLclTypeSIMD12(treeNode);
                break;
            }
#endif

            emitAttr size   = emitTypeSize(targetType);
            unsigned offs   = treeNode->gtLclFld.gtLclOffs;
            unsigned varNum = treeNode->gtLclVarCommon.gtLclNum;
            assert(varNum < compiler->lvaCount);

            emit->emitIns_R_S(ins_Move_Extend(targetType, treeNode->InReg()), size, targetReg, varNum, offs);
        }
            genProduceReg(treeNode);
            break;

        case GT_STORE_LCL_FLD:
        {
            noway_assert(targetType != TYP_STRUCT);
            noway_assert(!treeNode->InReg());
            assert(!varTypeIsFloating(targetType) || (targetType == treeNode->gtGetOp1()->TypeGet()));

#ifdef FEATURE_SIMD
            // storing of TYP_SIMD12 (i.e. Vector3) field
            if (treeNode->TypeGet() == TYP_SIMD12)
            {
                genStoreLclTypeSIMD12(treeNode);
                break;
            }
#endif // FEATURE_SIMD

            GenTreePtr op1 = treeNode->gtGetOp1();
            genConsumeRegs(op1);
            emit->emitInsBinary(ins_Store(targetType), emitTypeSize(treeNode), treeNode, op1);
        }
        break;

        case GT_STORE_LCL_VAR:
        {
            GenTreePtr op1 = treeNode->gtGetOp1();

            // var = call, where call returns a multi-reg return value
            // case is handled separately.
            if (op1->gtSkipReloadOrCopy()->IsMultiRegCall())
            {
                genMultiRegCallStoreToLocal(treeNode);
            }
            else
            {
                noway_assert(targetType != TYP_STRUCT);
                assert(!varTypeIsFloating(targetType) || (targetType == treeNode->gtGetOp1()->TypeGet()));

                unsigned   lclNum = treeNode->AsLclVarCommon()->gtLclNum;
                LclVarDsc* varDsc = &(compiler->lvaTable[lclNum]);

                // Ensure that lclVar nodes are typed correctly.
                assert(!varDsc->lvNormalizeOnStore() || treeNode->TypeGet() == genActualType(varDsc->TypeGet()));

#if !defined(_TARGET_64BIT_)
                if (treeNode->TypeGet() == TYP_LONG)
                {
                    genStoreLongLclVar(treeNode);
                    break;
                }
#endif // !defined(_TARGET_64BIT_)

#ifdef FEATURE_SIMD
                // storing of TYP_SIMD12 (i.e. Vector3) field
                if (treeNode->TypeGet() == TYP_SIMD12)
                {
                    genStoreLclTypeSIMD12(treeNode);
                    break;
                }

                if (varTypeIsSIMD(targetType) && (targetReg != REG_NA) && op1->IsCnsIntOrI())
                {
                    // This is only possible for a zero-init.
                    noway_assert(op1->IsIntegralConst(0));
                    genSIMDZero(targetType, varDsc->lvBaseType, targetReg);
                    genProduceReg(treeNode);
                    break;
                }
#endif // FEATURE_SIMD

                genConsumeRegs(op1);

                if (treeNode->gtRegNum == REG_NA)
                {
                    // stack store
                    emit->emitInsMov(ins_Store(targetType, compiler->isSIMDTypeLocalAligned(lclNum)),
                                     emitTypeSize(targetType), treeNode);
                    varDsc->lvRegNum = REG_STK;
                }
                else
                {
                    // Look for the case where we have a constant zero which we've marked for reuse,
                    // but which isn't actually in the register we want.  In that case, it's better to create
                    // zero in the target register, because an xor is smaller than a copy. Note that we could
                    // potentially handle this in the register allocator, but we can't always catch it there
                    // because the target may not have a register allocated for it yet.
                    if (op1->isUsedFromReg() && (op1->gtRegNum != treeNode->gtRegNum) &&
                        (op1->IsIntegralConst(0) || op1->IsFPZero()))
                    {
                        op1->gtRegNum = REG_NA;
                        op1->ResetReuseRegVal();
                    }

                    if (!op1->isUsedFromReg())
                    {
                        // Currently, we assume that the non-reg source of a GT_STORE_LCL_VAR writing to a register
                        // must be a constant. However, in the future we might want to support an operand used from
                        // memory.  This is a bit tricky because we have to decide it can be used from memory before
                        // register allocation,
                        // and this would be a case where, once that's done, we need to mark that node as always
                        // requiring a register - which we always assume now anyway, but once we "optimize" that
                        // we'll have to take cases like this into account.
                        assert((op1->gtRegNum == REG_NA) && op1->OperIsConst());
                        genSetRegToConst(treeNode->gtRegNum, targetType, op1);
                    }
                    else if (op1->gtRegNum != treeNode->gtRegNum)
                    {
                        assert(op1->gtRegNum != REG_NA);
                        emit->emitInsBinary(ins_Move_Extend(targetType, true), emitTypeSize(treeNode), treeNode, op1);
                    }
                }
            }

            if (treeNode->gtRegNum != REG_NA)
            {
                genProduceReg(treeNode);
            }
        }
        break;

        case GT_RETFILT:
            // A void GT_RETFILT is the end of a finally. For non-void filter returns we need to load the result in
            // the return register, if it's not already there. The processing is the same as GT_RETURN.
            if (targetType != TYP_VOID)
            {
                // For filters, the IL spec says the result is type int32. Further, the only specified legal values
                // are 0 or 1, with the use of other values "undefined".
                assert(targetType == TYP_INT);
            }

            __fallthrough;

        case GT_RETURN:
            genReturn(treeNode);
            break;

        case GT_LEA:
        {
            // if we are here, it is the case where there is an LEA that cannot
            // be folded into a parent instruction
            GenTreeAddrMode* lea = treeNode->AsAddrMode();
            genLeaInstruction(lea);
        }
        // genLeaInstruction calls genProduceReg()
        break;

        case GT_IND:
        {
#ifdef FEATURE_SIMD
            // Handling of Vector3 type values loaded through indirection.
            if (treeNode->TypeGet() == TYP_SIMD12)
            {
                genLoadIndTypeSIMD12(treeNode);
                break;
            }
#endif // FEATURE_SIMD

            GenTree* addr = treeNode->AsIndir()->Addr();
            if (addr->IsCnsIntOrI() && addr->IsIconHandle(GTF_ICON_TLS_HDL))
            {
                noway_assert(EA_ATTR(genTypeSize(treeNode->gtType)) == EA_PTRSIZE);
                emit->emitIns_R_C(ins_Load(TYP_I_IMPL), EA_PTRSIZE, treeNode->gtRegNum, FLD_GLOBAL_FS,
                                  (int)addr->gtIntCon.gtIconVal);
            }
            else
            {
                genConsumeAddress(addr);
                emit->emitInsMov(ins_Load(treeNode->TypeGet()), emitTypeSize(treeNode), treeNode);
            }
            genProduceReg(treeNode);
        }
        break;

        case GT_MULHI:
#ifdef _TARGET_X86_
        case GT_MUL_LONG:
#endif
            genCodeForMulHi(treeNode->AsOp());
            genProduceReg(treeNode);
            break;

        case GT_MUL:
        {
            instruction ins;
            emitAttr    size                  = emitTypeSize(treeNode);
            bool        isUnsignedMultiply    = ((treeNode->gtFlags & GTF_UNSIGNED) != 0);
            bool        requiresOverflowCheck = treeNode->gtOverflowEx();

            GenTree* op1 = treeNode->gtGetOp1();
            GenTree* op2 = treeNode->gtGetOp2();

            // there are 3 forms of x64 multiply:
            // 1-op form with 128 result:  RDX:RAX = RAX * rm
            // 2-op form: reg *= rm
            // 3-op form: reg = rm * imm

            genConsumeOperands(treeNode->AsOp());

            // This matches the 'mul' lowering in Lowering::SetMulOpCounts()
            //
            // immOp :: Only one operand can be an immediate
            // rmOp  :: Only one operand can be a memory op.
            // regOp :: A register op (especially the operand that matches 'targetReg')
            //          (can be nullptr when we have both a memory op and an immediate op)

            GenTree* immOp = nullptr;
            GenTree* rmOp  = op1;
            GenTree* regOp;

            if (op2->isContainedIntOrIImmed())
            {
                immOp = op2;
            }
            else if (op1->isContainedIntOrIImmed())
            {
                immOp = op1;
                rmOp  = op2;
            }

            if (immOp != nullptr)
            {
                // This must be a non-floating point operation.
                assert(!varTypeIsFloating(treeNode));

                // CQ: When possible use LEA for mul by imm 3, 5 or 9
                ssize_t imm = immOp->AsIntConCommon()->IconValue();

                if (!requiresOverflowCheck && rmOp->isUsedFromReg() && ((imm == 3) || (imm == 5) || (imm == 9)))
                {
                    // We will use the LEA instruction to perform this multiply
                    // Note that an LEA with base=x, index=x and scale=(imm-1) computes x*imm when imm=3,5 or 9.
                    unsigned int scale = (unsigned int)(imm - 1);
                    getEmitter()->emitIns_R_ARX(INS_lea, size, targetReg, rmOp->gtRegNum, rmOp->gtRegNum, scale, 0);
                }
                else
                {
                    // use the 3-op form with immediate
                    ins = getEmitter()->inst3opImulForReg(targetReg);
                    emit->emitInsBinary(ins, size, rmOp, immOp);
                }
            }
            else // we have no contained immediate operand
            {
                regOp = op1;
                rmOp  = op2;

                regNumber mulTargetReg = targetReg;
                if (isUnsignedMultiply && requiresOverflowCheck)
                {
                    ins          = INS_mulEAX;
                    mulTargetReg = REG_RAX;
                }
                else
                {
                    ins = genGetInsForOper(GT_MUL, targetType);
                }

                // Set rmOp to the memory operand (if any)
                // or set regOp to the op2 when it has the matching target register for our multiply op
                //
                if (op1->isUsedFromMemory() || (op2->isUsedFromReg() && (op2->gtRegNum == mulTargetReg)))
                {
                    regOp = op2;
                    rmOp  = op1;
                }
                assert(regOp->isUsedFromReg());

                // Setup targetReg when neither of the source operands was a matching register
                if (regOp->gtRegNum != mulTargetReg)
                {
                    inst_RV_RV(ins_Copy(targetType), mulTargetReg, regOp->gtRegNum, targetType);
                }

                emit->emitInsBinary(ins, size, treeNode, rmOp);

                // Move the result to the desired register, if necessary
                if ((ins == INS_mulEAX) && (targetReg != REG_RAX))
                {
                    inst_RV_RV(INS_mov, targetReg, REG_RAX, targetType);
                }
            }

            if (requiresOverflowCheck)
            {
                // Overflow checking is only used for non-floating point types
                noway_assert(!varTypeIsFloating(treeNode));

                genCheckOverflow(treeNode);
            }
        }
            genProduceReg(treeNode);
            break;

        case GT_MOD:
        case GT_UDIV:
        case GT_UMOD:
            // We shouldn't be seeing GT_MOD on float/double args as it should get morphed into a
            // helper call by front-end.  Similarly we shouldn't be seeing GT_UDIV and GT_UMOD
            // on float/double args.
            noway_assert(!varTypeIsFloating(treeNode));
            __fallthrough;

        case GT_DIV:
            genCodeForDivMod(treeNode->AsOp());
            break;

        case GT_INTRINSIC:
            genIntrinsic(treeNode);
            break;

#ifdef FEATURE_SIMD
        case GT_SIMD:
            genSIMDIntrinsic(treeNode->AsSIMD());
            break;
#endif // FEATURE_SIMD

        case GT_CKFINITE:
            genCkfinite(treeNode);
            break;

        case GT_EQ:
        case GT_NE:
        case GT_LT:
        case GT_LE:
        case GT_GE:
        case GT_GT:
        case GT_TEST_EQ:
        case GT_TEST_NE:
        {
            // TODO-XArch-CQ: Check if we can use the currently set flags.
            // TODO-XArch-CQ: Check for the case where we can simply transfer the carry bit to a register
            //         (signed < or >= where targetReg != REG_NA)

            GenTreePtr op1     = treeNode->gtGetOp1();
            var_types  op1Type = op1->TypeGet();

            if (varTypeIsFloating(op1Type))
            {
                genCompareFloat(treeNode);
            }
#if !defined(_TARGET_64BIT_)
            // X86 Long comparison
            else if (varTypeIsLong(op1Type))
            {
#ifdef DEBUG
                // The result of an unlowered long compare on a 32-bit target must either be
                // a) materialized into a register, or
                // b) unused.
                //
                // A long compare that has a result that is used but not materialized into a register should
                // have been handled by Lowering::LowerCompare.

                LIR::Use use;
                assert((treeNode->gtRegNum != REG_NA) || !LIR::AsRange(compiler->compCurBB).TryGetUse(treeNode, &use));
#endif
                genCompareLong(treeNode);
            }
#endif // !defined(_TARGET_64BIT_)
            else
            {
                genCompareInt(treeNode);
            }
        }
        break;

        case GT_JTRUE:
        {
            GenTree* cmp = treeNode->gtOp.gtOp1;

            assert(cmp->OperIsCompare());
            assert(compiler->compCurBB->bbJumpKind == BBJ_COND);

#if !defined(_TARGET_64BIT_)
            // Long-typed compares should have been handled by Lowering::LowerCompare.
            assert(!varTypeIsLong(cmp->gtGetOp1()));
#endif

            // Get the "kind" and type of the comparison.  Note that whether it is an unsigned cmp
            // is governed by a flag NOT by the inherent type of the node
            // TODO-XArch-CQ: Check if we can use the currently set flags.
            emitJumpKind jumpKind[2];
            bool         branchToTrueLabel[2];
            genJumpKindsForTree(cmp, jumpKind, branchToTrueLabel);

            BasicBlock* skipLabel = nullptr;
            if (jumpKind[0] != EJ_NONE)
            {
                BasicBlock* jmpTarget;
                if (branchToTrueLabel[0])
                {
                    jmpTarget = compiler->compCurBB->bbJumpDest;
                }
                else
                {
                    // This case arises only for ordered GT_EQ right now
                    assert((cmp->gtOper == GT_EQ) && ((cmp->gtFlags & GTF_RELOP_NAN_UN) == 0));
                    skipLabel = genCreateTempLabel();
                    jmpTarget = skipLabel;
                }

                inst_JMP(jumpKind[0], jmpTarget);
            }

            if (jumpKind[1] != EJ_NONE)
            {
                // the second conditional branch always has to be to the true label
                assert(branchToTrueLabel[1]);
                inst_JMP(jumpKind[1], compiler->compCurBB->bbJumpDest);
            }

            if (skipLabel != nullptr)
            {
                genDefineTempLabel(skipLabel);
            }
        }
        break;

        case GT_JCC:
        {
            GenTreeJumpCC* jcc = treeNode->AsJumpCC();

            assert(compiler->compCurBB->bbJumpKind == BBJ_COND);

            CompareKind  compareKind = ((jcc->gtFlags & GTF_UNSIGNED) != 0) ? CK_UNSIGNED : CK_SIGNED;
            emitJumpKind jumpKind    = genJumpKindForOper(jcc->gtCondition, compareKind);

            inst_JMP(jumpKind, compiler->compCurBB->bbJumpDest);
        }
        break;

        case GT_RETURNTRAP:
        {
            // this is nothing but a conditional call to CORINFO_HELP_STOP_FOR_GC
            // based on the contents of 'data'

            GenTree* data = treeNode->gtOp.gtOp1;
            genConsumeRegs(data);
            GenTreeIntCon cns = intForm(TYP_INT, 0);
            emit->emitInsBinary(INS_cmp, emitTypeSize(TYP_INT), data, &cns);

            BasicBlock* skipLabel = genCreateTempLabel();

            emitJumpKind jmpEqual = genJumpKindForOper(GT_EQ, CK_SIGNED);
            inst_JMP(jmpEqual, skipLabel);

            // emit the call to the EE-helper that stops for GC (or other reasons)
            regNumber tmpReg = treeNode->GetSingleTempReg();
            assert(genIsValidIntReg(tmpReg));

            genEmitHelperCall(CORINFO_HELP_STOP_FOR_GC, 0, EA_UNKNOWN, tmpReg);
            genDefineTempLabel(skipLabel);
        }
        break;

        case GT_STOREIND:
            genStoreInd(treeNode);
            break;

        case GT_COPY:
            // This is handled at the time we call genConsumeReg() on the GT_COPY
            break;

        case GT_SWAP:
        {
            // Swap is only supported for lclVar operands that are enregistered
            // We do not consume or produce any registers.  Both operands remain enregistered.
            // However, the gc-ness may change.
            assert(genIsRegCandidateLocal(treeNode->gtOp.gtOp1) && genIsRegCandidateLocal(treeNode->gtOp.gtOp2));

            GenTreeLclVarCommon* lcl1    = treeNode->gtOp.gtOp1->AsLclVarCommon();
            LclVarDsc*           varDsc1 = &(compiler->lvaTable[lcl1->gtLclNum]);
            var_types            type1   = varDsc1->TypeGet();
            GenTreeLclVarCommon* lcl2    = treeNode->gtOp.gtOp2->AsLclVarCommon();
            LclVarDsc*           varDsc2 = &(compiler->lvaTable[lcl2->gtLclNum]);
            var_types            type2   = varDsc2->TypeGet();

            // We must have both int or both fp regs
            assert(!varTypeIsFloating(type1) || varTypeIsFloating(type2));

            // FP swap is not yet implemented (and should have NYI'd in LSRA)
            assert(!varTypeIsFloating(type1));

            regNumber oldOp1Reg     = lcl1->gtRegNum;
            regMaskTP oldOp1RegMask = genRegMask(oldOp1Reg);
            regNumber oldOp2Reg     = lcl2->gtRegNum;
            regMaskTP oldOp2RegMask = genRegMask(oldOp2Reg);

            // We don't call genUpdateVarReg because we don't have a tree node with the new register.
            varDsc1->lvRegNum = oldOp2Reg;
            varDsc2->lvRegNum = oldOp1Reg;

            // Do the xchg
            emitAttr size = EA_PTRSIZE;
            if (varTypeGCtype(type1) != varTypeGCtype(type2))
            {
                // If the type specified to the emitter is a GC type, it will swap the GC-ness of the registers.
                // Otherwise it will leave them alone, which is correct if they have the same GC-ness.
                size = EA_GCREF;
            }
            inst_RV_RV(INS_xchg, oldOp1Reg, oldOp2Reg, TYP_I_IMPL, size);

            // Update the gcInfo.
            // Manually remove these regs for the gc sets (mostly to avoid confusing duplicative dump output)
            gcInfo.gcRegByrefSetCur &= ~(oldOp1RegMask | oldOp2RegMask);
            gcInfo.gcRegGCrefSetCur &= ~(oldOp1RegMask | oldOp2RegMask);

            // gcMarkRegPtrVal will do the appropriate thing for non-gc types.
            // It will also dump the updates.
            gcInfo.gcMarkRegPtrVal(oldOp2Reg, type1);
            gcInfo.gcMarkRegPtrVal(oldOp1Reg, type2);
        }
        break;

        case GT_LIST:
        case GT_FIELD_LIST:
        case GT_ARGPLACE:
            // Nothing to do
            break;

        case GT_PUTARG_STK:
            genPutArgStk(treeNode->AsPutArgStk());
            break;

        case GT_PUTARG_REG:
        {
#ifndef FEATURE_UNIX_AMD64_STRUCT_PASSING
            noway_assert(targetType != TYP_STRUCT);
#endif // FEATURE_UNIX_AMD64_STRUCT_PASSING
            // commas show up here commonly, as part of a nullchk operation
            GenTree* op1 = treeNode->gtOp.gtOp1;
            // If child node is not already in the register we need, move it
            genConsumeReg(op1);
            if (treeNode->gtRegNum != op1->gtRegNum)
            {
                inst_RV_RV(ins_Copy(targetType), treeNode->gtRegNum, op1->gtRegNum, targetType);
            }
            genProduceReg(treeNode);
        }
        break;

        case GT_CALL:
            genCallInstruction(treeNode->AsCall());
            break;

        case GT_JMP:
            genJmpMethod(treeNode);
            break;

        case GT_LOCKADD:
        case GT_XCHG:
        case GT_XADD:
            genLockedInstructions(treeNode->AsOp());
            break;

        case GT_MEMORYBARRIER:
            instGen_MemoryBarrier();
            break;

        case GT_CMPXCHG:
        {
            GenTreePtr location  = treeNode->gtCmpXchg.gtOpLocation;  // arg1
            GenTreePtr value     = treeNode->gtCmpXchg.gtOpValue;     // arg2
            GenTreePtr comparand = treeNode->gtCmpXchg.gtOpComparand; // arg3

            assert(location->gtRegNum != REG_NA && location->gtRegNum != REG_RAX);
            assert(value->gtRegNum != REG_NA && value->gtRegNum != REG_RAX);

            genConsumeReg(location);
            genConsumeReg(value);
            genConsumeReg(comparand);
            // comparand goes to RAX;
            // Note that we must issue this move after the genConsumeRegs(), in case any of the above
            // have a GT_COPY from RAX.
            if (comparand->gtRegNum != REG_RAX)
            {
                inst_RV_RV(ins_Copy(comparand->TypeGet()), REG_RAX, comparand->gtRegNum, comparand->TypeGet());
            }

            // location is Rm
            instGen(INS_lock);

            emit->emitIns_AR_R(INS_cmpxchg, emitTypeSize(targetType), value->gtRegNum, location->gtRegNum, 0);

            // Result is in RAX
            if (targetReg != REG_RAX)
            {
                inst_RV_RV(ins_Copy(targetType), targetReg, REG_RAX, targetType);
            }
        }
            genProduceReg(treeNode);
            break;

        case GT_RELOAD:
            // do nothing - reload is just a marker.
            // The parent node will call genConsumeReg on this which will trigger the unspill of this node's child
            // into the register specified in this node.
            break;

        case GT_NOP:
            break;

        case GT_NO_OP:
            if (treeNode->gtFlags & GTF_NO_OP_NO)
            {
                noway_assert(!"GTF_NO_OP_NO should not be set");
            }
            else
            {
                getEmitter()->emitIns_Nop(1);
            }
            break;

        case GT_ARR_BOUNDS_CHECK:
#ifdef FEATURE_SIMD
        case GT_SIMD_CHK:
#endif // FEATURE_SIMD
            genRangeCheck(treeNode);
            break;

        case GT_PHYSREG:
            if (treeNode->gtRegNum != treeNode->AsPhysReg()->gtSrcReg)
            {
                inst_RV_RV(INS_mov, treeNode->gtRegNum, treeNode->AsPhysReg()->gtSrcReg, targetType);

                genTransferRegGCState(treeNode->gtRegNum, treeNode->AsPhysReg()->gtSrcReg);
            }
            genProduceReg(treeNode);
            break;

        case GT_PHYSREGDST:
            break;

        case GT_NULLCHECK:
        {
            assert(treeNode->gtOp.gtOp1->isUsedFromReg());
            regNumber reg = genConsumeReg(treeNode->gtOp.gtOp1);
            emit->emitIns_AR_R(INS_cmp, EA_4BYTE, reg, reg, 0);
        }
        break;

        case GT_CATCH_ARG:

            noway_assert(handlerGetsXcptnObj(compiler->compCurBB->bbCatchTyp));

            /* Catch arguments get passed in a register. genCodeForBBlist()
               would have marked it as holding a GC object, but not used. */

            noway_assert(gcInfo.gcRegGCrefSetCur & RBM_EXCEPTION_OBJECT);
            genConsumeReg(treeNode);
            break;

#if !FEATURE_EH_FUNCLETS
        case GT_END_LFIN:

            // Have to clear the ShadowSP of the nesting level which encloses the finally. Generates:
            //     mov dword ptr [ebp-0xC], 0  // for some slot of the ShadowSP local var

            unsigned finallyNesting;
            finallyNesting = treeNode->gtVal.gtVal1;
            noway_assert(treeNode->gtVal.gtVal1 < compiler->compHndBBtabCount);
            noway_assert(finallyNesting < compiler->compHndBBtabCount);

            // The last slot is reserved for ICodeManager::FixContext(ppEndRegion)
            unsigned filterEndOffsetSlotOffs;
            PREFIX_ASSUME(compiler->lvaLclSize(compiler->lvaShadowSPslotsVar) >
                          TARGET_POINTER_SIZE); // below doesn't underflow.
            filterEndOffsetSlotOffs =
                (unsigned)(compiler->lvaLclSize(compiler->lvaShadowSPslotsVar) - TARGET_POINTER_SIZE);

            unsigned curNestingSlotOffs;
            curNestingSlotOffs = filterEndOffsetSlotOffs - ((finallyNesting + 1) * TARGET_POINTER_SIZE);
            instGen_Store_Imm_Into_Lcl(TYP_I_IMPL, EA_PTRSIZE, 0, compiler->lvaShadowSPslotsVar, curNestingSlotOffs);
            break;
#endif // !FEATURE_EH_FUNCLETS

        case GT_PINVOKE_PROLOG:
            noway_assert(((gcInfo.gcRegGCrefSetCur | gcInfo.gcRegByrefSetCur) & ~fullIntArgRegMask()) == 0);

            // the runtime side requires the codegen here to be consistent
            emit->emitDisableRandomNops();
            break;

        case GT_LABEL:
            genPendingCallLabel       = genCreateTempLabel();
            treeNode->gtLabel.gtLabBB = genPendingCallLabel;
            emit->emitIns_R_L(INS_lea, EA_PTR_DSP_RELOC, genPendingCallLabel, treeNode->gtRegNum);
            break;

        case GT_STORE_OBJ:
            if (treeNode->OperIsCopyBlkOp() && !treeNode->AsBlk()->gtBlkOpGcUnsafe)
            {
                assert(treeNode->AsObj()->gtGcPtrCount != 0);
                genCodeForCpObj(treeNode->AsObj());
                break;
            }
            __fallthrough;

        case GT_STORE_DYN_BLK:
        case GT_STORE_BLK:
            genCodeForStoreBlk(treeNode->AsBlk());
            break;

        case GT_JMPTABLE:
            genJumpTable(treeNode);
            break;

        case GT_SWITCH_TABLE:
            genTableBasedSwitch(treeNode);
            break;

        case GT_ARR_INDEX:
            genCodeForArrIndex(treeNode->AsArrIndex());
            break;

        case GT_ARR_OFFSET:
            genCodeForArrOffset(treeNode->AsArrOffs());
            break;

        case GT_CLS_VAR_ADDR:
            getEmitter()->emitIns_R_C(INS_lea, EA_PTRSIZE, targetReg, treeNode->gtClsVar.gtClsVarHnd, 0);
            genProduceReg(treeNode);
            break;

#if !defined(_TARGET_64BIT_)
        case GT_LONG:
            assert(treeNode->isUsedFromReg());
            genConsumeRegs(treeNode);
            break;
#endif

        case GT_IL_OFFSET:
            // Do nothing; these nodes are simply markers for debug info.
            break;

        default:
        {
#ifdef DEBUG
            char message[256];
            _snprintf_s(message, _countof(message), _TRUNCATE, "Unimplemented node type %s\n",
                        GenTree::NodeName(treeNode->OperGet()));
#endif
            assert(!"Unknown node in codegen");
        }
        break;
    }
}

//----------------------------------------------------------------------------------
// genMultiRegCallStoreToLocal: store multi-reg return value of a call node to a local
//
// Arguments:
//    treeNode  -  Gentree of GT_STORE_LCL_VAR
//
// Return Value:
//    None
//
// Assumption:
//    The child of store is a multi-reg call node.
//    genProduceReg() on treeNode is made by caller of this routine.
//
void CodeGen::genMultiRegCallStoreToLocal(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_STORE_LCL_VAR);

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING
    // Structs of size >=9 and <=16 are returned in two return registers on x64 Unix.
    assert(varTypeIsStruct(treeNode));

    // Assumption: current x64 Unix implementation requires that a multi-reg struct
    // var in 'var = call' is flagged as lvIsMultiRegRet to prevent it from
    // being struct promoted.
    unsigned   lclNum = treeNode->AsLclVarCommon()->gtLclNum;
    LclVarDsc* varDsc = &(compiler->lvaTable[lclNum]);
    noway_assert(varDsc->lvIsMultiRegRet);

    GenTree*     op1       = treeNode->gtGetOp1();
    GenTree*     actualOp1 = op1->gtSkipReloadOrCopy();
    GenTreeCall* call      = actualOp1->AsCall();
    assert(call->HasMultiRegRetVal());

    genConsumeRegs(op1);

    ReturnTypeDesc* retTypeDesc = call->GetReturnTypeDesc();
    assert(retTypeDesc->GetReturnRegCount() == MAX_RET_REG_COUNT);
    unsigned regCount = retTypeDesc->GetReturnRegCount();

    if (treeNode->gtRegNum != REG_NA)
    {
        // Right now the only enregistrable structs supported are SIMD types.
        assert(varTypeIsSIMD(treeNode));
        assert(varTypeIsFloating(retTypeDesc->GetReturnRegType(0)));
        assert(varTypeIsFloating(retTypeDesc->GetReturnRegType(1)));

        // This is a case of two 8-bytes that comprise the operand is in
        // two different xmm registers and needs to assembled into a single
        // xmm register.
        regNumber targetReg = treeNode->gtRegNum;
        regNumber reg0      = call->GetRegNumByIdx(0);
        regNumber reg1      = call->GetRegNumByIdx(1);

        if (op1->IsCopyOrReload())
        {
            // GT_COPY/GT_RELOAD will have valid reg for those positions
            // that need to be copied or reloaded.
            regNumber reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(0);
            if (reloadReg != REG_NA)
            {
                reg0 = reloadReg;
            }

            reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(1);
            if (reloadReg != REG_NA)
            {
                reg1 = reloadReg;
            }
        }

        if (targetReg != reg0 && targetReg != reg1)
        {
            // Copy reg0 into targetReg and let it to be handled by one
            // of the cases below.
            inst_RV_RV(ins_Copy(TYP_DOUBLE), targetReg, reg0, TYP_DOUBLE);
            targetReg = reg0;
        }

        if (targetReg == reg0)
        {
            // targeReg[63:0] = targetReg[63:0]
            // targetReg[127:64] = reg1[127:64]
            inst_RV_RV_IV(INS_shufpd, EA_16BYTE, targetReg, reg1, 0x00);
        }
        else
        {
            assert(targetReg == reg1);

            // We need two shuffles to achieve this
            // First:
            // targeReg[63:0] = targetReg[63:0]
            // targetReg[127:64] = reg0[63:0]
            //
            // Second:
            // targeReg[63:0] = targetReg[127:64]
            // targetReg[127:64] = targetReg[63:0]
            //
            // Essentially copy low 8-bytes from reg0 to high 8-bytes of targetReg
            // and next swap low and high 8-bytes of targetReg to have them
            // rearranged in the right order.
            inst_RV_RV_IV(INS_shufpd, EA_16BYTE, targetReg, reg0, 0x00);
            inst_RV_RV_IV(INS_shufpd, EA_16BYTE, targetReg, targetReg, 0x01);
        }
    }
    else
    {
        // Stack store
        int offset = 0;
        for (unsigned i = 0; i < regCount; ++i)
        {
            var_types type = retTypeDesc->GetReturnRegType(i);
            regNumber reg  = call->GetRegNumByIdx(i);
            if (op1->IsCopyOrReload())
            {
                // GT_COPY/GT_RELOAD will have valid reg for those positions
                // that need to be copied or reloaded.
                regNumber reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(i);
                if (reloadReg != REG_NA)
                {
                    reg = reloadReg;
                }
            }

            assert(reg != REG_NA);
            getEmitter()->emitIns_S_R(ins_Store(type), emitTypeSize(type), reg, lclNum, offset);
            offset += genTypeSize(type);
        }

        varDsc->lvRegNum = REG_STK;
    }
#elif defined(_TARGET_X86_)
    // Longs are returned in two return registers on x86.
    assert(varTypeIsLong(treeNode));

    // Assumption: current x86 implementation requires that a multi-reg long
    // var in 'var = call' is flagged as lvIsMultiRegRet to prevent it from
    // being promoted.
    unsigned   lclNum = treeNode->AsLclVarCommon()->gtLclNum;
    LclVarDsc* varDsc = &(compiler->lvaTable[lclNum]);
    noway_assert(varDsc->lvIsMultiRegRet);

    GenTree*     op1       = treeNode->gtGetOp1();
    GenTree*     actualOp1 = op1->gtSkipReloadOrCopy();
    GenTreeCall* call      = actualOp1->AsCall();
    assert(call->HasMultiRegRetVal());

    genConsumeRegs(op1);

    ReturnTypeDesc* retTypeDesc = call->GetReturnTypeDesc();
    unsigned        regCount    = retTypeDesc->GetReturnRegCount();
    assert(regCount == MAX_RET_REG_COUNT);

    // Stack store
    int offset = 0;
    for (unsigned i = 0; i < regCount; ++i)
    {
        var_types type = retTypeDesc->GetReturnRegType(i);
        regNumber reg  = call->GetRegNumByIdx(i);
        if (op1->IsCopyOrReload())
        {
            // GT_COPY/GT_RELOAD will have valid reg for those positions
            // that need to be copied or reloaded.
            regNumber reloadReg = op1->AsCopyOrReload()->GetRegNumByIdx(i);
            if (reloadReg != REG_NA)
            {
                reg = reloadReg;
            }
        }

        assert(reg != REG_NA);
        getEmitter()->emitIns_S_R(ins_Store(type), emitTypeSize(type), reg, lclNum, offset);
        offset += genTypeSize(type);
    }

    varDsc->lvRegNum            = REG_STK;
#else  // !FEATURE_UNIX_AMD64_STRUCT_PASSING && !_TARGET_X86_
    assert(!"Unreached");
#endif // !FEATURE_UNIX_AMD64_STRUCT_PASSING && !_TARGET_X86_
}

//------------------------------------------------------------------------
// genLclHeap: Generate code for localloc.
//
// Arguments:
//      tree - the localloc tree to generate.
//
// Notes:
//      Note that for x86, we don't track ESP movements while generating the localloc code.
//      The ESP tracking is used to report stack pointer-relative GC info, which is not
//      interesting while doing the localloc construction. Also, for functions with localloc,
//      we have EBP frames, and EBP-relative locals, and ESP-relative accesses only for function
//      call arguments. We store the ESP after the localloc is complete in the LocAllocSP
//      variable. This variable is implicitly reported to the VM in the GC info (its position
//      is defined by convention relative to other items), and is used by the GC to find the
//      "base" stack pointer in functions with localloc.
//
void CodeGen::genLclHeap(GenTreePtr tree)
{
    assert(tree->OperGet() == GT_LCLHEAP);
    assert(compiler->compLocallocUsed);

    GenTreePtr size = tree->gtOp.gtOp1;
    noway_assert((genActualType(size->gtType) == TYP_INT) || (genActualType(size->gtType) == TYP_I_IMPL));

    regNumber   targetReg = tree->gtRegNum;
    regNumber   regCnt    = REG_NA;
    var_types   type      = genActualType(size->gtType);
    emitAttr    easz      = emitTypeSize(type);
    BasicBlock* endLabel  = nullptr;

#ifdef DEBUG
    // Verify ESP
    if (compiler->opts.compStackCheckOnRet)
    {
        noway_assert(compiler->lvaReturnEspCheck != 0xCCCCCCCC &&
                     compiler->lvaTable[compiler->lvaReturnEspCheck].lvDoNotEnregister &&
                     compiler->lvaTable[compiler->lvaReturnEspCheck].lvOnFrame);
        getEmitter()->emitIns_S_R(INS_cmp, EA_PTRSIZE, REG_SPBASE, compiler->lvaReturnEspCheck, 0);

        BasicBlock*  esp_check = genCreateTempLabel();
        emitJumpKind jmpEqual  = genJumpKindForOper(GT_EQ, CK_SIGNED);
        inst_JMP(jmpEqual, esp_check);
        getEmitter()->emitIns(INS_BREAKPOINT);
        genDefineTempLabel(esp_check);
    }
#endif

    noway_assert(isFramePointerUsed()); // localloc requires Frame Pointer to be established since SP changes
    noway_assert(genStackLevel == 0);   // Can't have anything on the stack

    unsigned    stackAdjustment = 0;
    BasicBlock* loop            = nullptr;

    // compute the amount of memory to allocate to properly STACK_ALIGN.
    size_t amount = 0;
    if (size->IsCnsIntOrI())
    {
        // If size is a constant, then it must be contained.
        assert(size->isContained());

        // If amount is zero then return null in targetReg
        amount = size->gtIntCon.gtIconVal;
        if (amount == 0)
        {
            instGen_Set_Reg_To_Zero(EA_PTRSIZE, targetReg);
            goto BAILOUT;
        }

        // 'amount' is the total number of bytes to localloc to properly STACK_ALIGN
        amount = AlignUp(amount, STACK_ALIGN);
    }
    else
    {
        // The localloc requested memory size is non-constant.

        // Put the size value in targetReg. If it is zero, bail out by returning null in targetReg.
        genConsumeRegAndCopy(size, targetReg);
        endLabel = genCreateTempLabel();
        getEmitter()->emitIns_R_R(INS_test, easz, targetReg, targetReg);
        inst_JMP(EJ_je, endLabel);

        // Compute the size of the block to allocate and perform alignment.
        // If compInitMem=true, we can reuse targetReg as regcnt,
        // since we don't need any internal registers.
        if (compiler->info.compInitMem)
        {
            assert(tree->AvailableTempRegCount() == 0);
            regCnt = targetReg;
        }
        else
        {
            regCnt = tree->ExtractTempReg();
            if (regCnt != targetReg)
            {
                // Above, we put the size in targetReg. Now, copy it to our new temp register if necessary.
                inst_RV_RV(INS_mov, regCnt, targetReg, size->TypeGet());
            }
        }

        // Round up the number of bytes to allocate to a STACK_ALIGN boundary. This is done
        // by code like:
        //      add reg, 15
        //      and reg, -16
        // However, in the initialized memory case, we need the count of STACK_ALIGN-sized
        // elements, not a byte count, after the alignment. So instead of the "and", which
        // becomes unnecessary, generate a shift, e.g.:
        //      add reg, 15
        //      shr reg, 4

        inst_RV_IV(INS_add, regCnt, STACK_ALIGN - 1, emitActualTypeSize(type));

        if (compiler->info.compInitMem)
        {
            // Convert the count from a count of bytes to a loop count. We will loop once per
            // stack alignment size, so each loop will zero 4 bytes on x86 and 16 bytes on x64.
            // Note that we zero a single reg-size word per iteration on x86, and 2 reg-size
            // words per iteration on x64. We will shift off all the stack alignment bits
            // added above, so there is no need for an 'and' instruction.

            // --- shr regCnt, 2 (or 4) ---
            inst_RV_SH(INS_SHIFT_RIGHT_LOGICAL, EA_PTRSIZE, regCnt, STACK_ALIGN_SHIFT_ALL);
        }
        else
        {
            // Otherwise, mask off the low bits to align the byte count.
            inst_RV_IV(INS_AND, regCnt, ~(STACK_ALIGN - 1), emitActualTypeSize(type));
        }
    }

#if FEATURE_FIXED_OUT_ARGS
    // If we have an outgoing arg area then we must adjust the SP by popping off the
    // outgoing arg area. We will restore it right before we return from this method.
    //
    // Localloc returns stack space that aligned to STACK_ALIGN bytes. The following
    // are the cases that need to be handled:
    //   i) Method has out-going arg area.
    //      It is guaranteed that size of out-going arg area is STACK_ALIGN'ed (see fgMorphArgs).
    //      Therefore, we will pop off the out-going arg area from RSP before allocating the localloc space.
    //  ii) Method has no out-going arg area.
    //      Nothing to pop off from the stack.
    if (compiler->lvaOutgoingArgSpaceSize > 0)
    {
        assert((compiler->lvaOutgoingArgSpaceSize % STACK_ALIGN) == 0); // This must be true for the stack to remain
                                                                        // aligned
        inst_RV_IV(INS_add, REG_SPBASE, compiler->lvaOutgoingArgSpaceSize, EA_PTRSIZE);
        stackAdjustment += compiler->lvaOutgoingArgSpaceSize;
    }
#endif

    if (size->IsCnsIntOrI())
    {
        // We should reach here only for non-zero, constant size allocations.
        assert(amount > 0);
        assert((amount % STACK_ALIGN) == 0);
        assert((amount % REGSIZE_BYTES) == 0);

        // For small allocations we will generate up to six push 0 inline
        size_t cntRegSizedWords = amount / REGSIZE_BYTES;
        if (cntRegSizedWords <= 6)
        {
            for (; cntRegSizedWords != 0; cntRegSizedWords--)
            {
                inst_IV(INS_push_hide, 0); // push_hide means don't track the stack
            }
            goto ALLOC_DONE;
        }

        bool doNoInitLessThanOnePageAlloc =
            !compiler->info.compInitMem && (amount < compiler->eeGetPageSize()); // must be < not <=

#ifdef _TARGET_X86_
        bool needRegCntRegister = true;
#else  // !_TARGET_X86_
        bool needRegCntRegister = !doNoInitLessThanOnePageAlloc;
#endif // !_TARGET_X86_

        if (needRegCntRegister)
        {
            // If compInitMem=true, we can reuse targetReg as regcnt.
            // Since size is a constant, regCnt is not yet initialized.
            assert(regCnt == REG_NA);
            if (compiler->info.compInitMem)
            {
                assert(tree->AvailableTempRegCount() == 0);
                regCnt = targetReg;
            }
            else
            {
                regCnt = tree->ExtractTempReg();
            }
        }

        if (doNoInitLessThanOnePageAlloc)
        {
            // Since the size is less than a page, simply adjust ESP.
            // ESP might already be in the guard page, so we must touch it BEFORE
            // the alloc, not after.
            CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef _TARGET_X86_
            // For x86, we don't want to use "sub ESP" because we don't want the emitter to track the adjustment
            // to ESP. So do the work in the count register.
            // TODO-CQ: manipulate ESP directly, to share code, reduce #ifdefs, and improve CQ. This would require
            // creating a way to temporarily turn off the emitter's tracking of ESP, maybe marking instrDescs as "don't
            // track".
            inst_RV_RV(INS_mov, regCnt, REG_SPBASE, TYP_I_IMPL);
            getEmitter()->emitIns_AR_R(INS_TEST, EA_4BYTE, REG_SPBASE, REG_SPBASE, 0);
            inst_RV_IV(INS_sub, regCnt, amount, EA_PTRSIZE);
            inst_RV_RV(INS_mov, REG_SPBASE, regCnt, TYP_I_IMPL);
#else  // !_TARGET_X86_
            getEmitter()->emitIns_AR_R(INS_TEST, EA_4BYTE, REG_SPBASE, REG_SPBASE, 0);
            inst_RV_IV(INS_sub, REG_SPBASE, amount, EA_PTRSIZE);
#endif // !_TARGET_X86_

            goto ALLOC_DONE;
        }

        // else, "mov regCnt, amount"

        if (compiler->info.compInitMem)
        {
            // When initializing memory, we want 'amount' to be the loop count.
            assert((amount % STACK_ALIGN) == 0);
            amount /= STACK_ALIGN;
        }

        genSetRegToIcon(regCnt, amount, ((int)amount == amount) ? TYP_INT : TYP_LONG);
    }

    loop = genCreateTempLabel();
    if (compiler->info.compInitMem)
    {
        // At this point 'regCnt' is set to the number of loop iterations for this loop, if each
        // iteration zeros (and subtracts from the stack pointer) STACK_ALIGN bytes.
        // Since we have to zero out the allocated memory AND ensure that RSP is always valid
        // by tickling the pages, we will just push 0's on the stack.

        assert(genIsValidIntReg(regCnt));

        // Loop:
        genDefineTempLabel(loop);

        static_assert_no_msg((STACK_ALIGN % REGSIZE_BYTES) == 0);
        unsigned const count = (STACK_ALIGN / REGSIZE_BYTES);

        for (unsigned i = 0; i < count; i++)
        {
            inst_IV(INS_push_hide, 0); // --- push REG_SIZE bytes of 0
        }
        // Note that the stack must always be aligned to STACK_ALIGN bytes

        // Decrement the loop counter and loop if not done.
        inst_RV(INS_dec, regCnt, TYP_I_IMPL);
        inst_JMP(EJ_jne, loop);
    }
    else
    {
        // At this point 'regCnt' is set to the total number of bytes to localloc.
        //
        // We don't need to zero out the allocated memory. However, we do have
        // to tickle the pages to ensure that ESP is always valid and is
        // in sync with the "stack guard page".  Note that in the worst
        // case ESP is on the last byte of the guard page.  Thus you must
        // touch ESP+0 first not ESP+x01000.
        //
        // Another subtlety is that you don't want ESP to be exactly on the
        // boundary of the guard page because PUSH is predecrement, thus
        // call setup would not touch the guard page but just beyond it
        //
        // Note that we go through a few hoops so that ESP never points to
        // illegal pages at any time during the tickling process
        //
        //       neg   REGCNT
        //       add   REGCNT, ESP      // reg now holds ultimate ESP
        //       jb    loop             // result is smaller than orignial ESP (no wrap around)
        //       xor   REGCNT, REGCNT,  // Overflow, pick lowest possible number
        //  loop:
        //       test  ESP, [ESP+0]     // tickle the page
        //       mov   REGTMP, ESP
        //       sub   REGTMP, PAGE_SIZE
        //       mov   ESP, REGTMP
        //       cmp   ESP, REGCNT
        //       jae   loop
        //
        //       mov   ESP, REG
        //  end:
        inst_RV(INS_NEG, regCnt, TYP_I_IMPL);
        inst_RV_RV(INS_add, regCnt, REG_SPBASE, TYP_I_IMPL);
        inst_JMP(EJ_jb, loop);

        instGen_Set_Reg_To_Zero(EA_PTRSIZE, regCnt);

        genDefineTempLabel(loop);

        // Tickle the decremented value, and move back to ESP,
        // note that it has to be done BEFORE the update of ESP since
        // ESP might already be on the guard page.  It is OK to leave
        // the final value of ESP on the guard page
        getEmitter()->emitIns_AR_R(INS_TEST, EA_4BYTE, REG_SPBASE, REG_SPBASE, 0);

        // This is a harmless trick to avoid the emitter trying to track the
        // decrement of the ESP - we do the subtraction in another reg instead
        // of adjusting ESP directly.
        regNumber regTmp = tree->GetSingleTempReg();

        inst_RV_RV(INS_mov, regTmp, REG_SPBASE, TYP_I_IMPL);
        inst_RV_IV(INS_sub, regTmp, compiler->eeGetPageSize(), EA_PTRSIZE);
        inst_RV_RV(INS_mov, REG_SPBASE, regTmp, TYP_I_IMPL);

        inst_RV_RV(INS_cmp, REG_SPBASE, regCnt, TYP_I_IMPL);
        inst_JMP(EJ_jae, loop);

        // Move the final value to ESP
        inst_RV_RV(INS_mov, REG_SPBASE, regCnt);
    }

ALLOC_DONE:
    // Re-adjust SP to allocate out-going arg area
    if (stackAdjustment > 0)
    {
        assert((stackAdjustment % STACK_ALIGN) == 0); // This must be true for the stack to remain aligned
        inst_RV_IV(INS_sub, REG_SPBASE, stackAdjustment, EA_PTRSIZE);
    }

    // Return the stackalloc'ed address in result register.
    // TargetReg = RSP + stackAdjustment.
    getEmitter()->emitIns_R_AR(INS_lea, EA_PTRSIZE, targetReg, REG_SPBASE, stackAdjustment);

    if (endLabel != nullptr)
    {
        genDefineTempLabel(endLabel);
    }

BAILOUT:

    // Write the lvaLocAllocSPvar stack frame slot
    if (compiler->lvaLocAllocSPvar != BAD_VAR_NUM)
    {
        getEmitter()->emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, compiler->lvaLocAllocSPvar, 0);
    }

#if STACK_PROBES
    if (compiler->opts.compNeedStackProbes)
    {
        genGenerateStackProbe();
    }
#endif

#ifdef DEBUG
    // Update new ESP
    if (compiler->opts.compStackCheckOnRet)
    {
        noway_assert(compiler->lvaReturnEspCheck != 0xCCCCCCCC &&
                     compiler->lvaTable[compiler->lvaReturnEspCheck].lvDoNotEnregister &&
                     compiler->lvaTable[compiler->lvaReturnEspCheck].lvOnFrame);
        getEmitter()->emitIns_S_R(ins_Store(TYP_I_IMPL), EA_PTRSIZE, REG_SPBASE, compiler->lvaReturnEspCheck, 0);
    }
#endif

    genProduceReg(tree);
}

void CodeGen::genCodeForStoreBlk(GenTreeBlk* storeBlkNode)
{
#ifdef JIT32_GCENCODER
    assert(!storeBlkNode->gtBlkOpGcUnsafe);
#else
    if (storeBlkNode->gtBlkOpGcUnsafe)
    {
        getEmitter()->emitDisableGC();
    }
#endif // JIT32_GCENCODER

    bool isCopyBlk = storeBlkNode->OperIsCopyBlkOp();

    switch (storeBlkNode->gtBlkOpKind)
    {
#ifdef _TARGET_AMD64_
        case GenTreeBlk::BlkOpKindHelper:
            if (isCopyBlk)
            {
                genCodeForCpBlk(storeBlkNode);
            }
            else
            {
                genCodeForInitBlk(storeBlkNode);
            }
            break;
#endif // _TARGET_AMD64_
        case GenTreeBlk::BlkOpKindRepInstr:
            if (isCopyBlk)
            {
                genCodeForCpBlkRepMovs(storeBlkNode);
            }
            else
            {
                genCodeForInitBlkRepStos(storeBlkNode);
            }
            break;
        case GenTreeBlk::BlkOpKindUnroll:
            if (isCopyBlk)
            {
                genCodeForCpBlkUnroll(storeBlkNode);
            }
            else
            {
                genCodeForInitBlkUnroll(storeBlkNode);
            }
            break;
        default:
            unreached();
    }

#ifndef JIT32_GCENCODER
    if (storeBlkNode->gtBlkOpGcUnsafe)
    {
        getEmitter()->emitEnableGC();
    }
#endif // !defined(JIT32_GCENCODER)
}

//
//------------------------------------------------------------------------
// genCodeForInitBlkRepStos: Generate code for InitBlk using rep stos.
//
// Arguments:
//    initBlkNode - The Block store for which we are generating code.
//
// Preconditions:
//    On x64:
//      The size of the buffers must be a constant and also less than INITBLK_STOS_LIMIT bytes.
//      Any value larger than that, we'll use the helper even if both the fill byte and the
//      size are integer constants.
//  On x86:
//      The size must either be a non-constant or less than INITBLK_STOS_LIMIT bytes.
//
void CodeGen::genCodeForInitBlkRepStos(GenTreeBlk* initBlkNode)
{
    // Make sure we got the arguments of the initblk/initobj operation in the right registers.
    unsigned   size    = initBlkNode->Size();
    GenTreePtr dstAddr = initBlkNode->Addr();
    GenTreePtr initVal = initBlkNode->Data();
    if (initVal->OperIsInitVal())
    {
        initVal = initVal->gtGetOp1();
    }

#ifdef DEBUG
    assert(dstAddr->isUsedFromReg());
    assert(initVal->isUsedFromReg());
#ifdef _TARGET_AMD64_
    assert(size != 0);
#endif
    if (initVal->IsCnsIntOrI())
    {
#ifdef _TARGET_AMD64_
        assert(size > CPBLK_UNROLL_LIMIT && size < CPBLK_MOVS_LIMIT);
#else
        // Note that a size of zero means a non-constant size.
        assert((size == 0) || (size > CPBLK_UNROLL_LIMIT));
#endif
    }

#endif // DEBUG

    genConsumeBlockOp(initBlkNode, REG_RDI, REG_RAX, REG_RCX);
    instGen(INS_r_stosb);
}

// Generate code for InitBlk by performing a loop unroll
// Preconditions:
//   a) Both the size and fill byte value are integer constants.
//   b) The size of the struct to initialize is smaller than INITBLK_UNROLL_LIMIT bytes.
//
void CodeGen::genCodeForInitBlkUnroll(GenTreeBlk* initBlkNode)
{
    // Make sure we got the arguments of the initblk/initobj operation in the right registers
    unsigned   size    = initBlkNode->Size();
    GenTreePtr dstAddr = initBlkNode->Addr();
    GenTreePtr initVal = initBlkNode->Data();
    if (initVal->OperIsInitVal())
    {
        initVal = initVal->gtGetOp1();
    }

    assert(dstAddr->isUsedFromReg());
    assert(initVal->isUsedFromReg() || (initVal->IsIntegralConst(0) && ((size & 0xf) == 0)));
    assert(size != 0);
    assert(size <= INITBLK_UNROLL_LIMIT);
    assert(initVal->gtSkipReloadOrCopy()->IsCnsIntOrI());

    emitter* emit = getEmitter();

    genConsumeOperands(initBlkNode);

    // If the initVal was moved, or spilled and reloaded to a different register,
    // get the original initVal from below the GT_RELOAD, but only after capturing the valReg,
    // which needs to be the new register.
    regNumber valReg = initVal->gtRegNum;
    initVal          = initVal->gtSkipReloadOrCopy();

    unsigned offset = 0;

    // Perform an unroll using SSE2 loads and stores.
    if (size >= XMM_REGSIZE_BYTES)
    {
        regNumber tmpReg = initBlkNode->GetSingleTempReg();
        assert(genIsValidFloatReg(tmpReg));

        if (initVal->gtIntCon.gtIconVal != 0)
        {
            emit->emitIns_R_R(INS_mov_i2xmm, EA_PTRSIZE, tmpReg, valReg);
            emit->emitIns_R_R(INS_punpckldq, EA_8BYTE, tmpReg, tmpReg);
#ifdef _TARGET_X86_
            // For x86, we need one more to convert it from 8 bytes to 16 bytes.
            emit->emitIns_R_R(INS_punpckldq, EA_8BYTE, tmpReg, tmpReg);
#endif // _TARGET_X86_
        }
        else
        {
            emit->emitIns_R_R(INS_xorpd, EA_8BYTE, tmpReg, tmpReg);
        }

        // Determine how many 16 byte slots we're going to fill using SSE movs.
        size_t slots = size / XMM_REGSIZE_BYTES;

        while (slots-- > 0)
        {
            emit->emitIns_AR_R(INS_movdqu, EA_8BYTE, tmpReg, dstAddr->gtRegNum, offset);
            offset += XMM_REGSIZE_BYTES;
        }
    }

    // Fill the remainder (or a < 16 byte sized struct)
    if ((size & 8) != 0)
    {
#ifdef _TARGET_X86_
        // TODO-X86-CQ: [1091735] Revisit block ops codegen. One example: use movq for 8 byte movs.
        emit->emitIns_AR_R(INS_mov, EA_4BYTE, valReg, dstAddr->gtRegNum, offset);
        offset += 4;
        emit->emitIns_AR_R(INS_mov, EA_4BYTE, valReg, dstAddr->gtRegNum, offset);
        offset += 4;
#else // !_TARGET_X86_

        emit->emitIns_AR_R(INS_mov, EA_8BYTE, valReg, dstAddr->gtRegNum, offset);
        offset += 8;

#endif // !_TARGET_X86_
    }
    if ((size & 4) != 0)
    {
        emit->emitIns_AR_R(INS_mov, EA_4BYTE, valReg, dstAddr->gtRegNum, offset);
        offset += 4;
    }
    if ((size & 2) != 0)
    {
        emit->emitIns_AR_R(INS_mov, EA_2BYTE, valReg, dstAddr->gtRegNum, offset);
        offset += 2;
    }
    if ((size & 1) != 0)
    {
        emit->emitIns_AR_R(INS_mov, EA_1BYTE, valReg, dstAddr->gtRegNum, offset);
    }
}

// Generates code for InitBlk by calling the VM memset helper function.
// Preconditions:
// a) The size argument of the InitBlk is not an integer constant.
// b) The size argument of the InitBlk is >= INITBLK_STOS_LIMIT bytes.
void CodeGen::genCodeForInitBlk(GenTreeBlk* initBlkNode)
{
#ifdef _TARGET_AMD64_
    // Make sure we got the arguments of the initblk operation in the right registers
    unsigned   blockSize = initBlkNode->Size();
    GenTreePtr dstAddr   = initBlkNode->Addr();
    GenTreePtr initVal   = initBlkNode->Data();
    if (initVal->OperIsInitVal())
    {
        initVal = initVal->gtGetOp1();
    }

    assert(dstAddr->isUsedFromReg());
    assert(initVal->isUsedFromReg());

    if (blockSize != 0)
    {
        assert(blockSize >= CPBLK_MOVS_LIMIT);
    }

    genConsumeBlockOp(initBlkNode, REG_ARG_0, REG_ARG_1, REG_ARG_2);

    genEmitHelperCall(CORINFO_HELP_MEMSET, 0, EA_UNKNOWN);
#else  // !_TARGET_AMD64_
    NYI_X86("Helper call for InitBlk");
#endif // !_TARGET_AMD64_
}

// Generate code for a load from some address + offset
//   baseNode: tree node which can be either a local address or arbitrary node
//   offset: distance from the baseNode from which to load
void CodeGen::genCodeForLoadOffset(instruction ins, emitAttr size, regNumber dst, GenTree* baseNode, unsigned offset)
{
    emitter* emit = getEmitter();

    if (baseNode->OperIsLocalAddr())
    {
        if (baseNode->gtOper == GT_LCL_FLD_ADDR)
        {
            offset += baseNode->gtLclFld.gtLclOffs;
        }
        emit->emitIns_R_S(ins, size, dst, baseNode->gtLclVarCommon.gtLclNum, offset);
    }
    else
    {
        emit->emitIns_R_AR(ins, size, dst, baseNode->gtRegNum, offset);
    }
}

//------------------------------------------------------------------------
// genCodeForStoreOffset: Generate code to store a reg to [base + offset].
//
// Arguments:
//      ins         - the instruction to generate.
//      size        - the size that needs to be stored.
//      src         - the register which needs to be stored.
//      baseNode    - the base, relative to which to store the src register.
//      offset      - the offset that is added to the baseNode to calculate the address to store into.
//
void CodeGen::genCodeForStoreOffset(instruction ins, emitAttr size, regNumber src, GenTree* baseNode, unsigned offset)
{
    emitter* emit = getEmitter();

    if (baseNode->OperIsLocalAddr())
    {
        if (baseNode->gtOper == GT_LCL_FLD_ADDR)
        {
            offset += baseNode->gtLclFld.gtLclOffs;
        }

        emit->emitIns_S_R(ins, size, src, baseNode->AsLclVarCommon()->GetLclNum(), offset);
    }
    else
    {
        emit->emitIns_AR_R(ins, size, src, baseNode->gtRegNum, offset);
    }
}

// Generates CpBlk code by performing a loop unroll
// Preconditions:
//  The size argument of the CpBlk node is a constant and <= 64 bytes.
//  This may seem small but covers >95% of the cases in several framework assemblies.
//
void CodeGen::genCodeForCpBlkUnroll(GenTreeBlk* cpBlkNode)
{
    // Make sure we got the arguments of the cpblk operation in the right registers
    unsigned   size    = cpBlkNode->Size();
    GenTreePtr dstAddr = cpBlkNode->Addr();
    GenTreePtr source  = cpBlkNode->Data();
    GenTreePtr srcAddr = nullptr;
    assert(size <= CPBLK_UNROLL_LIMIT);

    emitter* emit = getEmitter();

    if (source->gtOper == GT_IND)
    {
        srcAddr = source->gtGetOp1();
        if (srcAddr->isUsedFromReg())
        {
            genConsumeReg(srcAddr);
        }
    }
    else
    {
        noway_assert(source->IsLocal());
        // TODO-Cleanup: Consider making the addrForm() method in Rationalize public, e.g. in GenTree.
        // OR: transform source to GT_IND(GT_LCL_VAR_ADDR)
        if (source->OperGet() == GT_LCL_VAR)
        {
            source->SetOper(GT_LCL_VAR_ADDR);
        }
        else
        {
            assert(source->OperGet() == GT_LCL_FLD);
            source->SetOper(GT_LCL_FLD_ADDR);
        }
        srcAddr = source;
    }

    if (dstAddr->isUsedFromReg())
    {
        genConsumeReg(dstAddr);
    }

    unsigned offset = 0;

    // If the size of this struct is larger than 16 bytes
    // let's use SSE2 to be able to do 16 byte at a time
    // loads and stores.

    if (size >= XMM_REGSIZE_BYTES)
    {
        regNumber xmmReg = cpBlkNode->GetSingleTempReg(RBM_ALLFLOAT);
        assert(genIsValidFloatReg(xmmReg));
        size_t slots = size / XMM_REGSIZE_BYTES;

        // TODO: In the below code the load and store instructions are for 16 bytes, but the
        //       type is EA_8BYTE. The movdqa/u are 16 byte instructions, so it works, but
        //       this probably needs to be changed.
        while (slots-- > 0)
        {
            // Load
            genCodeForLoadOffset(INS_movdqu, EA_8BYTE, xmmReg, srcAddr, offset);
            // Store
            genCodeForStoreOffset(INS_movdqu, EA_8BYTE, xmmReg, dstAddr, offset);
            offset += XMM_REGSIZE_BYTES;
        }
    }

    // Fill the remainder (15 bytes or less) if there's one.
    if ((size & 0xf) != 0)
    {
        // Grab the integer temp register to emit the remaining loads and stores.
        regNumber tmpReg = cpBlkNode->GetSingleTempReg(RBM_ALLINT);

        if ((size & 8) != 0)
        {
#ifdef _TARGET_X86_
            // TODO-X86-CQ: [1091735] Revisit block ops codegen. One example: use movq for 8 byte movs.
            for (unsigned savedOffs = offset; offset < savedOffs + 8; offset += 4)
            {
                genCodeForLoadOffset(INS_mov, EA_4BYTE, tmpReg, srcAddr, offset);
                genCodeForStoreOffset(INS_mov, EA_4BYTE, tmpReg, dstAddr, offset);
            }
#else  // !_TARGET_X86_
            genCodeForLoadOffset(INS_mov, EA_8BYTE, tmpReg, srcAddr, offset);
            genCodeForStoreOffset(INS_mov, EA_8BYTE, tmpReg, dstAddr, offset);
            offset += 8;
#endif // !_TARGET_X86_
        }
        if ((size & 4) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_4BYTE, tmpReg, srcAddr, offset);
            genCodeForStoreOffset(INS_mov, EA_4BYTE, tmpReg, dstAddr, offset);
            offset += 4;
        }
        if ((size & 2) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_2BYTE, tmpReg, srcAddr, offset);
            genCodeForStoreOffset(INS_mov, EA_2BYTE, tmpReg, dstAddr, offset);
            offset += 2;
        }
        if ((size & 1) != 0)
        {
            genCodeForLoadOffset(INS_mov, EA_1BYTE, tmpReg, srcAddr, offset);
            genCodeForStoreOffset(INS_mov, EA_1BYTE, tmpReg, dstAddr, offset);
        }
    }
}

// Generate code for CpBlk by using rep movs
// Preconditions:
// The size argument of the CpBlk is a constant and is between
// CPBLK_UNROLL_LIMIT and CPBLK_MOVS_LIMIT bytes.
void CodeGen::genCodeForCpBlkRepMovs(GenTreeBlk* cpBlkNode)
{
    // Make sure we got the arguments of the cpblk operation in the right registers
    unsigned   size    = cpBlkNode->Size();
    GenTreePtr dstAddr = cpBlkNode->Addr();
    GenTreePtr source  = cpBlkNode->Data();
    GenTreePtr srcAddr = nullptr;

#ifdef DEBUG
    assert(dstAddr->isUsedFromReg());
    assert(source->isContained());

#ifdef _TARGET_X86_
    if (size == 0)
    {
        noway_assert(cpBlkNode->OperGet() == GT_STORE_DYN_BLK);
    }
    else
#endif
    {
#ifdef _TARGET_X64_
        assert(size > CPBLK_UNROLL_LIMIT && size < CPBLK_MOVS_LIMIT);
#else
        assert(size > CPBLK_UNROLL_LIMIT);
#endif
    }
#endif // DEBUG

    genConsumeBlockOp(cpBlkNode, REG_RDI, REG_RSI, REG_RCX);
    instGen(INS_r_movsb);
}

#ifdef FEATURE_PUT_STRUCT_ARG_STK
//------------------------------------------------------------------------
// CodeGen::genMove8IfNeeded: Conditionally move 8 bytes of a struct to the argument area
//
// Arguments:
//    size       - The size of bytes remaining to be moved
//    longTmpReg - The tmp register to be used for the long value
//    srcAddr    - The address of the source struct
//    offset     - The current offset being copied
//
// Return Value:
//    Returns the number of bytes moved (8 or 0).
//
// Notes:
//    This is used in the PutArgStkKindUnroll case, to move any bytes that are
//    not an even multiple of 16.
//    On x86, longTmpReg must be an xmm reg; on x64 it must be an integer register.
//    This is checked by genStoreRegToStackArg.
//
unsigned CodeGen::genMove8IfNeeded(unsigned size, regNumber longTmpReg, GenTree* srcAddr, unsigned offset)
{
#ifdef _TARGET_X86_
    instruction longMovIns = INS_movq;
#else  // !_TARGET_X86_
    instruction longMovIns = INS_mov;
#endif // !_TARGET_X86_
    if ((size & 8) != 0)
    {
        genCodeForLoadOffset(longMovIns, EA_8BYTE, longTmpReg, srcAddr, offset);
        genStoreRegToStackArg(TYP_LONG, longTmpReg, offset);
        return 8;
    }
    return 0;
}

//------------------------------------------------------------------------
// CodeGen::genMove4IfNeeded: Conditionally move 4 bytes of a struct to the argument area
//
// Arguments:
//    size      - The size of bytes remaining to be moved
//    intTmpReg - The tmp register to be used for the long value
//    srcAddr   - The address of the source struct
//    offset    - The current offset being copied
//
// Return Value:
//    Returns the number of bytes moved (4 or 0).
//
// Notes:
//    This is used in the PutArgStkKindUnroll case, to move any bytes that are
//    not an even multiple of 16.
//    intTmpReg must be an integer register.
//    This is checked by genStoreRegToStackArg.
//
unsigned CodeGen::genMove4IfNeeded(unsigned size, regNumber intTmpReg, GenTree* srcAddr, unsigned offset)
{
    if ((size & 4) != 0)
    {
        genCodeForLoadOffset(INS_mov, EA_4BYTE, intTmpReg, srcAddr, offset);
        genStoreRegToStackArg(TYP_INT, intTmpReg, offset);
        return 4;
    }
    return 0;
}

//------------------------------------------------------------------------
// CodeGen::genMove2IfNeeded: Conditionally move 2 bytes of a struct to the argument area
//
// Arguments:
//    size      - The size of bytes remaining to be moved
//    intTmpReg - The tmp register to be used for the long value
//    srcAddr   - The address of the source struct
//    offset    - The current offset being copied
//
// Return Value:
//    Returns the number of bytes moved (2 or 0).
//
// Notes:
//    This is used in the PutArgStkKindUnroll case, to move any bytes that are
//    not an even multiple of 16.
//    intTmpReg must be an integer register.
//    This is checked by genStoreRegToStackArg.
//
unsigned CodeGen::genMove2IfNeeded(unsigned size, regNumber intTmpReg, GenTree* srcAddr, unsigned offset)
{
    if ((size & 2) != 0)
    {
        genCodeForLoadOffset(INS_mov, EA_2BYTE, intTmpReg, srcAddr, offset);
        genStoreRegToStackArg(TYP_SHORT, intTmpReg, offset);
        return 2;
    }
    return 0;
}

//------------------------------------------------------------------------
// CodeGen::genMove1IfNeeded: Conditionally move 1 byte of a struct to the argument area
//
// Arguments:
//    size      - The size of bytes remaining to be moved
//    intTmpReg - The tmp register to be used for the long value
//    srcAddr   - The address of the source struct
//    offset    - The current offset being copied
//
// Return Value:
//    Returns the number of bytes moved (1 or 0).
//
// Notes:
//    This is used in the PutArgStkKindUnroll case, to move any bytes that are
//    not an even multiple of 16.
//    intTmpReg must be an integer register.
//    This is checked by genStoreRegToStackArg.
//
unsigned CodeGen::genMove1IfNeeded(unsigned size, regNumber intTmpReg, GenTree* srcAddr, unsigned offset)
{

    if ((size & 1) != 0)
    {
        genCodeForLoadOffset(INS_mov, EA_1BYTE, intTmpReg, srcAddr, offset);
        genStoreRegToStackArg(TYP_BYTE, intTmpReg, offset);
        return 1;
    }
    return 0;
}

//---------------------------------------------------------------------------------------------------------------//
// genStructPutArgUnroll: Generates code for passing a struct arg on stack by value using loop unrolling.
//
// Arguments:
//     putArgNode  - the PutArgStk tree.
//
// Notes:
//     m_stkArgVarNum must be set to the base var number, relative to which the by-val struct will be copied to the
//     stack.
//
// TODO-Amd64-Unix: Try to share code with copyblk.
//      Need refactoring of copyblk before it could be used for putarg_stk.
//      The difference for now is that a putarg_stk contains its children, while cpyblk does not.
//      This creates differences in code. After some significant refactoring it could be reused.
//
void CodeGen::genStructPutArgUnroll(GenTreePutArgStk* putArgNode)
{
    // We will never call this method for SIMD types, which are stored directly
    // in genPutStructArgStk().
    noway_assert(putArgNode->TypeGet() == TYP_STRUCT);

    // Make sure we got the arguments of the cpblk operation in the right registers
    GenTreePtr dstAddr = putArgNode;
    GenTreePtr src     = putArgNode->gtOp.gtOp1;

    unsigned size = putArgNode->getArgSize();
    assert(size <= CPBLK_UNROLL_LIMIT);

    emitter* emit         = getEmitter();
    unsigned putArgOffset = putArgNode->getArgOffset();

    assert(src->isContained());

    assert(src->gtOper == GT_OBJ);

    if (src->gtOp.gtOp1->isUsedFromReg())
    {
        genConsumeReg(src->gtOp.gtOp1);
    }

    unsigned offset = 0;

    regNumber xmmTmpReg  = REG_NA;
    regNumber intTmpReg  = REG_NA;
    regNumber longTmpReg = REG_NA;
#ifdef _TARGET_X86_
    // On x86 we use an XMM register for both 16 and 8-byte chunks, but if it's
    // less than 16 bytes, we will just be using pushes
    if (size >= 8)
    {
        xmmTmpReg  = putArgNode->GetSingleTempReg(RBM_ALLFLOAT);
        longTmpReg = xmmTmpReg;
    }
    if ((size & 0x7) != 0)
    {
        intTmpReg = putArgNode->GetSingleTempReg(RBM_ALLINT);
    }
#else  // !_TARGET_X86_
    // On x64 we use an XMM register only for 16-byte chunks.
    if (size >= XMM_REGSIZE_BYTES)
    {
        xmmTmpReg = putArgNode->GetSingleTempReg(RBM_ALLFLOAT);
    }
    if ((size & 0xf) != 0)
    {
        intTmpReg  = putArgNode->GetSingleTempReg(RBM_ALLINT);
        longTmpReg = intTmpReg;
    }
#endif // !_TARGET_X86_

    // If the size of this struct is larger than 16 bytes
    // let's use SSE2 to be able to do 16 byte at a time
    // loads and stores.
    if (size >= XMM_REGSIZE_BYTES)
    {
#ifdef _TARGET_X86_
        assert(!m_pushStkArg);
#endif // _TARGET_X86_
        size_t slots = size / XMM_REGSIZE_BYTES;

        assert(putArgNode->gtGetOp1()->isContained());
        assert(putArgNode->gtGetOp1()->gtOp.gtOper == GT_OBJ);

        // TODO: In the below code the load and store instructions are for 16 bytes, but the
        //          type is EA_8BYTE. The movdqa/u are 16 byte instructions, so it works, but
        //          this probably needs to be changed.
        while (slots-- > 0)
        {
            // Load
            genCodeForLoadOffset(INS_movdqu, EA_8BYTE, xmmTmpReg, src->gtGetOp1(), offset);

            // Store
            genStoreRegToStackArg(TYP_STRUCT, xmmTmpReg, offset);

            offset += XMM_REGSIZE_BYTES;
        }
    }

    // Fill the remainder (15 bytes or less) if there's one.
    if ((size & 0xf) != 0)
    {
#ifdef _TARGET_X86_
        if (m_pushStkArg)
        {
            // This case is currently supported only for the case where the total size is
            // less than XMM_REGSIZE_BYTES. We need to push the remaining chunks in reverse
            // order. However, morph has ensured that we have a struct that is an even
            // multiple of TARGET_POINTER_SIZE, so we don't need to worry about alignment.
            assert(((size & 0xc) == size) && (offset == 0));
            // If we have a 4 byte chunk, load it from either offset 0 or 8, depending on
            // whether we've got an 8 byte chunk, and then push it on the stack.
            unsigned pushedBytes = genMove4IfNeeded(size, intTmpReg, src->gtOp.gtOp1, size & 0x8);
            // Now if we have an 8 byte chunk, load it from offset 0 (it's the first chunk)
            // and push it on the stack.
            pushedBytes += genMove8IfNeeded(size, longTmpReg, src->gtOp.gtOp1, 0);
        }
        else
#endif // _TARGET_X86_
        {
            offset += genMove8IfNeeded(size, longTmpReg, src->gtOp.gtOp1, offset);
            offset += genMove4IfNeeded(size, intTmpReg, src->gtOp.gtOp1, offset);
            offset += genMove2IfNeeded(size, intTmpReg, src->gtOp.gtOp1, offset);
            offset += genMove1IfNeeded(size, intTmpReg, src->gtOp.gtOp1, offset);
            assert(offset == size);
        }
    }
}

//------------------------------------------------------------------------
// genStructPutArgRepMovs: Generates code for passing a struct arg by value on stack using Rep Movs.
//
// Arguments:
//     putArgNode  - the PutArgStk tree.
//
// Preconditions:
//     The size argument of the PutArgStk (for structs) is a constant and is between
//     CPBLK_UNROLL_LIMIT and CPBLK_MOVS_LIMIT bytes.
//     m_stkArgVarNum must be set to the base var number, relative to which the by-val struct bits will go.
//
void CodeGen::genStructPutArgRepMovs(GenTreePutArgStk* putArgNode)
{
    assert(putArgNode->TypeGet() == TYP_STRUCT);
    assert(putArgNode->getArgSize() > CPBLK_UNROLL_LIMIT);

    // Make sure we got the arguments of the cpblk operation in the right registers
    GenTreePtr dstAddr = putArgNode;
    GenTreePtr srcAddr = putArgNode->gtGetOp1();

    // Validate state.
    assert(putArgNode->gtRsvdRegs == (RBM_RDI | RBM_RCX | RBM_RSI));
    assert(srcAddr->isContained());

    genConsumePutStructArgStk(putArgNode, REG_RDI, REG_RSI, REG_RCX);
    instGen(INS_r_movsb);
}

//------------------------------------------------------------------------
// If any Vector3 args are on stack and they are not pass-by-ref, the upper 32bits
// must be cleared to zeroes. The native compiler doesn't clear the upper bits
// and there is no way to know if the caller is native or not. So, the upper
// 32 bits of Vector argument on stack are always cleared to zero.
#if defined(FEATURE_UNIX_AMD64_STRUCT_PASSING) && defined(FEATURE_SIMD)
void CodeGen::genClearStackVec3ArgUpperBits()
{
#ifdef DEBUG
    if (verbose)
    {
        printf("*************** In genClearStackVec3ArgUpperBits()\n");
    }
#endif

    assert(compiler->compGeneratingProlog);

    unsigned varNum = 0;

    for (unsigned varNum = 0; varNum < compiler->info.compArgsCount; varNum++)
    {
        LclVarDsc* varDsc = &(compiler->lvaTable[varNum]);
        assert(varDsc->lvIsParam);

        // Does var has simd12 type?
        if (varDsc->lvType != TYP_SIMD12)
        {
            continue;
        }

        if (!varDsc->lvIsRegArg)
        {
            // Clear the upper 32 bits by mov dword ptr [V_ARG_BASE+0xC], 0
            getEmitter()->emitIns_S_I(ins_Store(TYP_INT), EA_4BYTE, varNum, genTypeSize(TYP_FLOAT) * 3, 0);
        }
        else
        {
            // Assume that for x64 linux, an argument is fully in registers
            // or fully on stack.
            regNumber argReg = varDsc->GetOtherArgReg();

            // Clear the upper 32 bits by two shift instructions.
            // argReg = argReg << 96
            getEmitter()->emitIns_R_I(INS_pslldq, emitActualTypeSize(TYP_SIMD12), argReg, 12);
            // argReg = argReg >> 96
            getEmitter()->emitIns_R_I(INS_psrldq, emitActualTypeSize(TYP_SIMD12), argReg, 12);
        }
    }
}
#endif // defined(FEATURE_UNIX_AMD64_STRUCT_PASSING) && defined(FEATURE_SIMD)
#endif // FEATURE_PUT_STRUCT_ARG_STK

// Generate code for CpObj nodes wich copy structs that have interleaved
// GC pointers.
// This will generate a sequence of movsp instructions for the cases of non-gc members.
// Note that movsp is an alias for movsd on x86 and movsq on x64.
// and calls to the BY_REF_ASSIGN helper otherwise.
void CodeGen::genCodeForCpObj(GenTreeObj* cpObjNode)
{
    // Make sure we got the arguments of the cpobj operation in the right registers
    GenTreePtr dstAddr       = cpObjNode->Addr();
    GenTreePtr source        = cpObjNode->Data();
    GenTreePtr srcAddr       = nullptr;
    var_types  srcAddrType   = TYP_BYREF;
    bool       sourceIsLocal = false;

    assert(source->isContained());
    if (source->gtOper == GT_IND)
    {
        srcAddr = source->gtGetOp1();
        assert(srcAddr->isUsedFromReg());
    }
    else
    {
        noway_assert(source->IsLocal());
        sourceIsLocal = true;
    }

    bool dstOnStack = dstAddr->OperIsLocalAddr();

#ifdef DEBUG

    assert(dstAddr->isUsedFromReg());

    // If the GenTree node has data about GC pointers, this means we're dealing
    // with CpObj, so this requires special logic.
    assert(cpObjNode->gtGcPtrCount > 0);

    // MovSp (alias for movsq on x64 and movsd on x86) instruction is used for copying non-gcref fields
    // and it needs src = RSI and dst = RDI.
    // Either these registers must not contain lclVars, or they must be dying or marked for spill.
    // This is because these registers are incremented as we go through the struct.
    if (!sourceIsLocal)
    {
        GenTree* actualSrcAddr    = srcAddr->gtSkipReloadOrCopy();
        GenTree* actualDstAddr    = dstAddr->gtSkipReloadOrCopy();
        unsigned srcLclVarNum     = BAD_VAR_NUM;
        unsigned dstLclVarNum     = BAD_VAR_NUM;
        bool     isSrcAddrLiveOut = false;
        bool     isDstAddrLiveOut = false;
        if (genIsRegCandidateLocal(actualSrcAddr))
        {
            srcLclVarNum     = actualSrcAddr->AsLclVarCommon()->gtLclNum;
            isSrcAddrLiveOut = ((actualSrcAddr->gtFlags & (GTF_VAR_DEATH | GTF_SPILL)) == 0);
        }
        if (genIsRegCandidateLocal(actualDstAddr))
        {
            dstLclVarNum     = actualDstAddr->AsLclVarCommon()->gtLclNum;
            isDstAddrLiveOut = ((actualDstAddr->gtFlags & (GTF_VAR_DEATH | GTF_SPILL)) == 0);
        }
        assert((actualSrcAddr->gtRegNum != REG_RSI) || !isSrcAddrLiveOut ||
               ((srcLclVarNum == dstLclVarNum) && !isDstAddrLiveOut));
        assert((actualDstAddr->gtRegNum != REG_RDI) || !isDstAddrLiveOut ||
               ((srcLclVarNum == dstLclVarNum) && !isSrcAddrLiveOut));
        srcAddrType = srcAddr->TypeGet();
    }
#endif // DEBUG

    // Consume the operands and get them into the right registers.
    // They may now contain gc pointers (depending on their type; gcMarkRegPtrVal will "do the right thing").
    genConsumeBlockOp(cpObjNode, REG_RDI, REG_RSI, REG_NA);
    gcInfo.gcMarkRegPtrVal(REG_RSI, srcAddrType);
    gcInfo.gcMarkRegPtrVal(REG_RDI, dstAddr->TypeGet());

    unsigned slots = cpObjNode->gtSlots;

    // If we can prove it's on the stack we don't need to use the write barrier.
    if (dstOnStack)
    {
        if (slots >= CPOBJ_NONGC_SLOTS_LIMIT)
        {
            // If the destination of the CpObj is on the stack, make sure we allocated
            // RCX to emit the movsp (alias for movsd or movsq for 32 and 64 bits respectively).
            assert((cpObjNode->gtRsvdRegs & RBM_RCX) != 0);

            getEmitter()->emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, slots);
            instGen(INS_r_movsp);
        }
        else
        {
            // For small structs, it's better to emit a sequence of movsp than to
            // emit a rep movsp instruction.
            while (slots > 0)
            {
                instGen(INS_movsp);
                slots--;
            }
        }
    }
    else
    {
        BYTE*    gcPtrs     = cpObjNode->gtGcPtrs;
        unsigned gcPtrCount = cpObjNode->gtGcPtrCount;

        unsigned i = 0;
        while (i < slots)
        {
            switch (gcPtrs[i])
            {
                case TYPE_GC_NONE:
                    // Let's see if we can use rep movsp instead of a sequence of movsp instructions
                    // to save cycles and code size.
                    {
                        unsigned nonGcSlotCount = 0;

                        do
                        {
                            nonGcSlotCount++;
                            i++;
                        } while (i < slots && gcPtrs[i] == TYPE_GC_NONE);

                        // If we have a very small contiguous non-gc region, it's better just to
                        // emit a sequence of movsp instructions
                        if (nonGcSlotCount < CPOBJ_NONGC_SLOTS_LIMIT)
                        {
                            while (nonGcSlotCount > 0)
                            {
                                instGen(INS_movsp);
                                nonGcSlotCount--;
                            }
                        }
                        else
                        {
                            // Otherwise, we can save code-size and improve CQ by emitting
                            // rep movsp (alias for movsd/movsq for x86/x64)
                            assert((cpObjNode->gtRsvdRegs & RBM_RCX) != 0);

                            getEmitter()->emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, nonGcSlotCount);
                            instGen(INS_r_movsp);
                        }
                    }
                    break;
                default:
                    // We have a GC pointer, call the memory barrier.
                    genEmitHelperCall(CORINFO_HELP_ASSIGN_BYREF, 0, EA_PTRSIZE);
                    gcPtrCount--;
                    i++;
            }
        }

        assert(gcPtrCount == 0);
    }

    // Clear the gcInfo for RSI and RDI.
    // While we normally update GC info prior to the last instruction that uses them,
    // these actually live into the helper call.
    gcInfo.gcMarkRegSetNpt(RBM_RSI);
    gcInfo.gcMarkRegSetNpt(RBM_RDI);
}

// Generate code for a CpBlk node by the means of the VM memcpy helper call
// Preconditions:
// a) The size argument of the CpBlk is not an integer constant
// b) The size argument is a constant but is larger than CPBLK_MOVS_LIMIT bytes.
void CodeGen::genCodeForCpBlk(GenTreeBlk* cpBlkNode)
{
#ifdef _TARGET_AMD64_
    // Make sure we got the arguments of the cpblk operation in the right registers
    unsigned   blockSize = cpBlkNode->Size();
    GenTreePtr dstAddr   = cpBlkNode->Addr();
    GenTreePtr source    = cpBlkNode->Data();
    GenTreePtr srcAddr   = nullptr;

    // Size goes in arg2
    if (blockSize != 0)
    {
        assert(blockSize >= CPBLK_MOVS_LIMIT);
        assert((cpBlkNode->gtRsvdRegs & RBM_ARG_2) != 0);
    }
    else
    {
        noway_assert(cpBlkNode->gtOper == GT_STORE_DYN_BLK);
    }

    // Source address goes in arg1
    if (source->gtOper == GT_IND)
    {
        srcAddr = source->gtGetOp1();
        assert(srcAddr->isUsedFromReg());
    }
    else
    {
        noway_assert(source->IsLocal());
        assert((cpBlkNode->gtRsvdRegs & RBM_ARG_1) != 0);
        inst_RV_TT(INS_lea, REG_ARG_1, source, 0, EA_BYREF);
    }

    genConsumeBlockOp(cpBlkNode, REG_ARG_0, REG_ARG_1, REG_ARG_2);

    genEmitHelperCall(CORINFO_HELP_MEMCPY, 0, EA_UNKNOWN);
#else  // !_TARGET_AMD64_
    noway_assert(false && "Helper call for CpBlk is not needed.");
#endif // !_TARGET_AMD64_
}

// generate code do a switch statement based on a table of ip-relative offsets
void CodeGen::genTableBasedSwitch(GenTree* treeNode)
{
    genConsumeOperands(treeNode->AsOp());
    regNumber idxReg  = treeNode->gtOp.gtOp1->gtRegNum;
    regNumber baseReg = treeNode->gtOp.gtOp2->gtRegNum;

    regNumber tmpReg = treeNode->GetSingleTempReg();

    // load the ip-relative offset (which is relative to start of fgFirstBB)
    getEmitter()->emitIns_R_ARX(INS_mov, EA_4BYTE, baseReg, baseReg, idxReg, 4, 0);

    // add it to the absolute address of fgFirstBB
    compiler->fgFirstBB->bbFlags |= BBF_JMP_TARGET;
    getEmitter()->emitIns_R_L(INS_lea, EA_PTR_DSP_RELOC, compiler->fgFirstBB, tmpReg);
    getEmitter()->emitIns_R_R(INS_add, EA_PTRSIZE, baseReg, tmpReg);
    // jmp baseReg
    getEmitter()->emitIns_R(INS_i_jmp, emitTypeSize(TYP_I_IMPL), baseReg);
}

// emits the table and an instruction to get the address of the first element
void CodeGen::genJumpTable(GenTree* treeNode)
{
    noway_assert(compiler->compCurBB->bbJumpKind == BBJ_SWITCH);
    assert(treeNode->OperGet() == GT_JMPTABLE);

    unsigned     jumpCount = compiler->compCurBB->bbJumpSwt->bbsCount;
    BasicBlock** jumpTable = compiler->compCurBB->bbJumpSwt->bbsDstTab;
    unsigned     jmpTabOffs;
    unsigned     jmpTabBase;

    jmpTabBase = getEmitter()->emitBBTableDataGenBeg(jumpCount, true);

    jmpTabOffs = 0;

    JITDUMP("\n      J_M%03u_DS%02u LABEL   DWORD\n", Compiler::s_compMethodsCount, jmpTabBase);

    for (unsigned i = 0; i < jumpCount; i++)
    {
        BasicBlock* target = *jumpTable++;
        noway_assert(target->bbFlags & BBF_JMP_TARGET);

        JITDUMP("            DD      L_M%03u_BB%02u\n", Compiler::s_compMethodsCount, target->bbNum);

        getEmitter()->emitDataGenData(i, target);
    };

    getEmitter()->emitDataGenEnd();

    // Access to inline data is 'abstracted' by a special type of static member
    // (produced by eeFindJitDataOffs) which the emitter recognizes as being a reference
    // to constant data, not a real static field.
    getEmitter()->emitIns_R_C(INS_lea, emitTypeSize(TYP_I_IMPL), treeNode->gtRegNum,
                              compiler->eeFindJitDataOffs(jmpTabBase), 0);
    genProduceReg(treeNode);
}

// generate code for the locked operations:
// GT_LOCKADD, GT_XCHG, GT_XADD
void CodeGen::genLockedInstructions(GenTreeOp* treeNode)
{
    GenTree*    data      = treeNode->gtOp.gtOp2;
    GenTree*    addr      = treeNode->gtOp.gtOp1;
    regNumber   targetReg = treeNode->gtRegNum;
    regNumber   dataReg   = data->gtRegNum;
    regNumber   addrReg   = addr->gtRegNum;
    var_types   type      = genActualType(data->TypeGet());
    instruction ins;

    // The register allocator should have extended the lifetime of the address
    // so that it is not used as the target.
    noway_assert(addrReg != targetReg);

    // If data is a lclVar that's not a last use, we'd better have allocated a register
    // for the result (except in the case of GT_LOCKADD which does not produce a register result).
    assert(targetReg != REG_NA || treeNode->OperGet() == GT_LOCKADD || !genIsRegCandidateLocal(data) ||
           (data->gtFlags & GTF_VAR_DEATH) != 0);

    genConsumeOperands(treeNode);
    if (targetReg != REG_NA && dataReg != REG_NA && dataReg != targetReg)
    {
        inst_RV_RV(ins_Copy(type), targetReg, dataReg);
        data->gtRegNum = targetReg;

        // TODO-XArch-Cleanup: Consider whether it is worth it, for debugging purposes, to restore the
        // original gtRegNum on data, after calling emitInsBinary below.
    }
    switch (treeNode->OperGet())
    {
        case GT_LOCKADD:
            instGen(INS_lock);
            ins = INS_add;
            break;
        case GT_XCHG:
            // lock is implied by xchg
            ins = INS_xchg;
            break;
        case GT_XADD:
            instGen(INS_lock);
            ins = INS_xadd;
            break;
        default:
            unreached();
    }

    // all of these nodes implicitly do an indirection on op1
    // so create a temporary node to feed into the pattern matching
    GenTreeIndir i = indirForm(type, addr);
    getEmitter()->emitInsBinary(ins, emitTypeSize(type), &i, data);

    if (treeNode->gtRegNum != REG_NA)
    {
        genProduceReg(treeNode);
    }
}

// generate code for BoundsCheck nodes
void CodeGen::genRangeCheck(GenTreePtr oper)
{
#ifdef FEATURE_SIMD
    noway_assert(oper->OperGet() == GT_ARR_BOUNDS_CHECK || oper->OperGet() == GT_SIMD_CHK);
#else  // !FEATURE_SIMD
    noway_assert(oper->OperGet() == GT_ARR_BOUNDS_CHECK);
#endif // !FEATURE_SIMD

    GenTreeBoundsChk* bndsChk = oper->AsBoundsChk();

    GenTreePtr arrIndex  = bndsChk->gtIndex;
    GenTreePtr arrLen    = bndsChk->gtArrLen;
    GenTreePtr arrRef    = nullptr;
    int        lenOffset = 0;

    GenTree *    src1, *src2;
    emitJumpKind jmpKind;

    genConsumeRegs(arrIndex);
    genConsumeRegs(arrLen);

    if (arrIndex->isContainedIntOrIImmed())
    {
        // arrIndex is a contained constant.  In this case
        // we will generate one of the following
        //      cmp [mem], immed    (if arrLen is a memory op)
        //      cmp reg, immed      (if arrLen is in a reg)
        //
        // That is arrLen cannot be a contained immed.
        assert(!arrLen->isContainedIntOrIImmed());

        src1    = arrLen;
        src2    = arrIndex;
        jmpKind = EJ_jbe;
    }
    else
    {
        // arrIndex could either be a contained memory op or a reg
        // In this case we will generate one of the following
        //      cmp  [mem], immed   (if arrLen is a constant)
        //      cmp  [mem], reg     (if arrLen is in a reg)
        //      cmp  reg, immed     (if arrIndex is in a reg)
        //      cmp  reg1, reg2     (if arraIndex is in reg1)
        //      cmp  reg, [mem]     (if arrLen is a memory op)
        //
        // That is only one of arrIndex or arrLen can be a memory op.
        assert(!arrIndex->isUsedFromMemory() || !arrLen->isUsedFromMemory());

        src1    = arrIndex;
        src2    = arrLen;
        jmpKind = EJ_jae;
    }

    var_types bndsChkType = src2->TypeGet();
#if DEBUG
    // Bounds checks can only be 32 or 64 bit sized comparisons.
    assert(bndsChkType == TYP_INT || bndsChkType == TYP_LONG);

    // The type of the bounds check should always wide enough to compare against the index.
    assert(emitTypeSize(bndsChkType) >= emitTypeSize(src1->TypeGet()));
#endif // DEBUG

    getEmitter()->emitInsBinary(INS_cmp, emitTypeSize(bndsChkType), src1, src2);
    genJumpToThrowHlpBlk(jmpKind, bndsChk->gtThrowKind, bndsChk->gtIndRngFailBB);
}

//------------------------------------------------------------------------
// genOffsetOfMDArrayLowerBound: Returns the offset from the Array object to the
//   lower bound for the given dimension.
//
// Arguments:
//    elemType  - the element type of the array
//    rank      - the rank of the array
//    dimension - the dimension for which the lower bound offset will be returned.
//
// Return Value:
//    The offset.

unsigned CodeGen::genOffsetOfMDArrayLowerBound(var_types elemType, unsigned rank, unsigned dimension)
{
    // Note that the lower bound and length fields of the Array object are always TYP_INT, even on 64-bit targets.
    return compiler->eeGetArrayDataOffset(elemType) + genTypeSize(TYP_INT) * (dimension + rank);
}

//------------------------------------------------------------------------
// genOffsetOfMDArrayLength: Returns the offset from the Array object to the
//   size for the given dimension.
//
// Arguments:
//    elemType  - the element type of the array
//    rank      - the rank of the array
//    dimension - the dimension for which the lower bound offset will be returned.
//
// Return Value:
//    The offset.

unsigned CodeGen::genOffsetOfMDArrayDimensionSize(var_types elemType, unsigned rank, unsigned dimension)
{
    // Note that the lower bound and length fields of the Array object are always TYP_INT, even on 64-bit targets.
    return compiler->eeGetArrayDataOffset(elemType) + genTypeSize(TYP_INT) * dimension;
}

//------------------------------------------------------------------------
// genCodeForArrIndex: Generates code to bounds check the index for one dimension of an array reference,
//                     producing the effective index by subtracting the lower bound.
//
// Arguments:
//    arrIndex - the node for which we're generating code
//
// Return Value:
//    None.
//

void CodeGen::genCodeForArrIndex(GenTreeArrIndex* arrIndex)
{
    GenTreePtr arrObj    = arrIndex->ArrObj();
    GenTreePtr indexNode = arrIndex->IndexExpr();

    regNumber arrReg   = genConsumeReg(arrObj);
    regNumber indexReg = genConsumeReg(indexNode);
    regNumber tgtReg   = arrIndex->gtRegNum;

    unsigned  dim      = arrIndex->gtCurrDim;
    unsigned  rank     = arrIndex->gtArrRank;
    var_types elemType = arrIndex->gtArrElemType;

    noway_assert(tgtReg != REG_NA);

    // Subtract the lower bound for this dimension.
    // TODO-XArch-CQ: make this contained if it's an immediate that fits.
    if (tgtReg != indexReg)
    {
        inst_RV_RV(INS_mov, tgtReg, indexReg, indexNode->TypeGet());
    }
    getEmitter()->emitIns_R_AR(INS_sub, emitActualTypeSize(TYP_INT), tgtReg, arrReg,
                               genOffsetOfMDArrayLowerBound(elemType, rank, dim));
    getEmitter()->emitIns_R_AR(INS_cmp, emitActualTypeSize(TYP_INT), tgtReg, arrReg,
                               genOffsetOfMDArrayDimensionSize(elemType, rank, dim));
    genJumpToThrowHlpBlk(EJ_jae, SCK_RNGCHK_FAIL);

    genProduceReg(arrIndex);
}

//------------------------------------------------------------------------
// genCodeForArrOffset: Generates code to compute the flattened array offset for
//    one dimension of an array reference:
//        result = (prevDimOffset * dimSize) + effectiveIndex
//    where dimSize is obtained from the arrObj operand
//
// Arguments:
//    arrOffset - the node for which we're generating code
//
// Return Value:
//    None.
//
// Notes:
//    dimSize and effectiveIndex are always non-negative, the former by design,
//    and the latter because it has been normalized to be zero-based.

void CodeGen::genCodeForArrOffset(GenTreeArrOffs* arrOffset)
{
    GenTreePtr offsetNode = arrOffset->gtOffset;
    GenTreePtr indexNode  = arrOffset->gtIndex;
    GenTreePtr arrObj     = arrOffset->gtArrObj;

    regNumber tgtReg = arrOffset->gtRegNum;
    assert(tgtReg != REG_NA);

    unsigned  dim      = arrOffset->gtCurrDim;
    unsigned  rank     = arrOffset->gtArrRank;
    var_types elemType = arrOffset->gtArrElemType;

    // First, consume the operands in the correct order.
    regNumber offsetReg = REG_NA;
    regNumber tmpReg    = REG_NA;
    if (!offsetNode->IsIntegralConst(0))
    {
        offsetReg = genConsumeReg(offsetNode);

        // We will use a temp register for the offset*scale+effectiveIndex computation.
        tmpReg = arrOffset->GetSingleTempReg();
    }
    else
    {
        assert(offsetNode->isContained());
    }
    regNumber indexReg = genConsumeReg(indexNode);
    // Although arrReg may not be used in the constant-index case, if we have generated
    // the value into a register, we must consume it, otherwise we will fail to end the
    // live range of the gc ptr.
    // TODO-CQ: Currently arrObj will always have a register allocated to it.
    // We could avoid allocating a register for it, which would be of value if the arrObj
    // is an on-stack lclVar.
    regNumber arrReg = REG_NA;
    if (arrObj->gtHasReg())
    {
        arrReg = genConsumeReg(arrObj);
    }

    if (!offsetNode->IsIntegralConst(0))
    {
        assert(tmpReg != REG_NA);
        assert(arrReg != REG_NA);

        // Evaluate tgtReg = offsetReg*dim_size + indexReg.
        // tmpReg is used to load dim_size and the result of the multiplication.
        // Note that dim_size will never be negative.

        getEmitter()->emitIns_R_AR(INS_mov, emitActualTypeSize(TYP_INT), tmpReg, arrReg,
                                   genOffsetOfMDArrayDimensionSize(elemType, rank, dim));
        inst_RV_RV(INS_imul, tmpReg, offsetReg);

        if (tmpReg == tgtReg)
        {
            inst_RV_RV(INS_add, tmpReg, indexReg);
        }
        else
        {
            if (indexReg != tgtReg)
            {
                inst_RV_RV(INS_mov, tgtReg, indexReg, TYP_I_IMPL);
            }
            inst_RV_RV(INS_add, tgtReg, tmpReg);
        }
    }
    else
    {
        if (indexReg != tgtReg)
        {
            inst_RV_RV(INS_mov, tgtReg, indexReg, TYP_INT);
        }
    }
    genProduceReg(arrOffset);
}

// make a temporary indir we can feed to pattern matching routines
// in cases where we don't want to instantiate all the indirs that happen
//
GenTreeIndir CodeGen::indirForm(var_types type, GenTree* base)
{
    GenTreeIndir i(GT_IND, type, base, nullptr);
    i.gtRegNum = REG_NA;
    // has to be nonnull (because contained nodes can't be the last in block)
    // but don't want it to be a valid pointer
    i.gtNext = (GenTree*)(-1);
    return i;
}

// make a temporary int we can feed to pattern matching routines
// in cases where we don't want to instantiate
//
GenTreeIntCon CodeGen::intForm(var_types type, ssize_t value)
{
    GenTreeIntCon i(type, value);
    i.gtRegNum = REG_NA;
    // has to be nonnull (because contained nodes can't be the last in block)
    // but don't want it to be a valid pointer
    i.gtNext = (GenTree*)(-1);
    return i;
}

instruction CodeGen::genGetInsForOper(genTreeOps oper, var_types type)
{
    instruction ins;

    // Operations on SIMD vectors shouldn't come this path
    assert(!varTypeIsSIMD(type));
    if (varTypeIsFloating(type))
    {
        return ins_MathOp(oper, type);
    }

    switch (oper)
    {
        case GT_ADD:
            ins = INS_add;
            break;
        case GT_AND:
            ins = INS_and;
            break;
        case GT_LSH:
            ins = INS_shl;
            break;
        case GT_MUL:
            ins = INS_imul;
            break;
        case GT_NEG:
            ins = INS_neg;
            break;
        case GT_NOT:
            ins = INS_not;
            break;
        case GT_OR:
            ins = INS_or;
            break;
        case GT_ROL:
            ins = INS_rol;
            break;
        case GT_ROR:
            ins = INS_ror;
            break;
        case GT_RSH:
            ins = INS_sar;
            break;
        case GT_RSZ:
            ins = INS_shr;
            break;
        case GT_SUB:
            ins = INS_sub;
            break;
        case GT_XOR:
            ins = INS_xor;
            break;
#if !defined(_TARGET_64BIT_)
        case GT_ADD_LO:
            ins = INS_add;
            break;
        case GT_ADD_HI:
            ins = INS_adc;
            break;
        case GT_SUB_LO:
            ins = INS_sub;
            break;
        case GT_SUB_HI:
            ins = INS_sbb;
            break;
        case GT_LSH_HI:
            ins = INS_shld;
            break;
        case GT_RSH_LO:
            ins = INS_shrd;
            break;
#endif // !defined(_TARGET_64BIT_)
        default:
            unreached();
            break;
    }
    return ins;
}

//------------------------------------------------------------------------
// genCodeForShift: Generates the code sequence for a GenTree node that
// represents a bit shift or rotate operation (<<, >>, >>>, rol, ror).
//
// Arguments:
//    tree - the bit shift node (that specifies the type of bit shift to perform).
//
// Assumptions:
//    a) All GenTrees are register allocated.
//    b) The shift-by-amount in tree->gtOp.gtOp2 is either a contained constant or
//       it's a register-allocated expression. If it is in a register that is
//       not RCX, it will be moved to RCX (so RCX better not be in use!).
//
void CodeGen::genCodeForShift(GenTreePtr tree)
{
    // Only the non-RMW case here.
    assert(tree->OperIsShiftOrRotate());
    assert(tree->gtOp.gtOp1->isUsedFromReg());
    assert(tree->gtRegNum != REG_NA);

    genConsumeOperands(tree->AsOp());

    var_types   targetType = tree->TypeGet();
    instruction ins        = genGetInsForOper(tree->OperGet(), targetType);

    GenTreePtr operand    = tree->gtGetOp1();
    regNumber  operandReg = operand->gtRegNum;

    GenTreePtr shiftBy = tree->gtGetOp2();

    if (shiftBy->isContainedIntOrIImmed())
    {
        // First, move the operand to the destination register and
        // later on perform the shift in-place.
        // (LSRA will try to avoid this situation through preferencing.)
        if (tree->gtRegNum != operandReg)
        {
            inst_RV_RV(INS_mov, tree->gtRegNum, operandReg, targetType);
        }

        int shiftByValue = (int)shiftBy->AsIntConCommon()->IconValue();
        inst_RV_SH(ins, emitTypeSize(tree), tree->gtRegNum, shiftByValue);
    }
    else
    {
        // We must have the number of bits to shift stored in ECX, since we constrained this node to
        // sit in ECX. In case this didn't happen, LSRA expects the code generator to move it since it's a single
        // register destination requirement.
        genCopyRegIfNeeded(shiftBy, REG_RCX);

        // The operand to be shifted must not be in ECX
        noway_assert(operandReg != REG_RCX);

        if (tree->gtRegNum != operandReg)
        {
            inst_RV_RV(INS_mov, tree->gtRegNum, operandReg, targetType);
        }
        inst_RV_CL(ins, tree->gtRegNum, targetType);
    }

    genProduceReg(tree);
}

#ifdef _TARGET_X86_
//------------------------------------------------------------------------
// genCodeForShiftLong: Generates the code sequence for a GenTree node that
// represents a three operand bit shift or rotate operation (<<Hi, >>Lo).
//
// Arguments:
//    tree - the bit shift node (that specifies the type of bit shift to perform).
//
// Assumptions:
//    a) All GenTrees are register allocated.
//    b) The shift-by-amount in tree->gtOp.gtOp2 is a contained constant
//
void CodeGen::genCodeForShiftLong(GenTreePtr tree)
{
    // Only the non-RMW case here.
    genTreeOps oper = tree->OperGet();
    assert(oper == GT_LSH_HI || oper == GT_RSH_LO);

    GenTree* operand = tree->gtOp.gtOp1;
    assert(operand->OperGet() == GT_LONG);
    assert(operand->gtOp.gtOp1->isUsedFromReg());
    assert(operand->gtOp.gtOp2->isUsedFromReg());

    GenTree* operandLo = operand->gtGetOp1();
    GenTree* operandHi = operand->gtGetOp2();

    regNumber regLo = operandLo->gtRegNum;
    regNumber regHi = operandHi->gtRegNum;

    genConsumeOperands(tree->AsOp());

    var_types   targetType = tree->TypeGet();
    instruction ins        = genGetInsForOper(oper, targetType);

    GenTreePtr shiftBy = tree->gtGetOp2();

    assert(shiftBy->isContainedIntOrIImmed());

    unsigned int count = shiftBy->AsIntConCommon()->IconValue();

    regNumber regResult = (oper == GT_LSH_HI) ? regHi : regLo;

    if (regResult != tree->gtRegNum)
    {
        inst_RV_RV(INS_mov, tree->gtRegNum, regResult, targetType);
    }

    if (oper == GT_LSH_HI)
    {
        inst_RV_RV_IV(ins, emitTypeSize(targetType), tree->gtRegNum, regLo, count);
    }
    else
    {
        assert(oper == GT_RSH_LO);
        inst_RV_RV_IV(ins, emitTypeSize(targetType), tree->gtRegNum, regHi, count);
    }

    genProduceReg(tree);
}
#endif

//------------------------------------------------------------------------
// genCodeForShiftRMW: Generates the code sequence for a GT_STOREIND GenTree node that
// represents a RMW bit shift or rotate operation (<<, >>, >>>, rol, ror), for example:
//      GT_STOREIND( AddressTree, GT_SHL( Ind ( AddressTree ), Operand ) )
//
// Arguments:
//    storeIndNode: the GT_STOREIND node.
//
void CodeGen::genCodeForShiftRMW(GenTreeStoreInd* storeInd)
{
    GenTree* data = storeInd->Data();
    GenTree* addr = storeInd->Addr();

    assert(data->OperIsShiftOrRotate());

    // This function only handles the RMW case.
    assert(data->gtOp.gtOp1->isUsedFromMemory());
    assert(data->gtOp.gtOp1->isIndir());
    assert(Lowering::IndirsAreEquivalent(data->gtOp.gtOp1, storeInd));
    assert(data->gtRegNum == REG_NA);

    var_types   targetType = data->TypeGet();
    genTreeOps  oper       = data->OperGet();
    instruction ins        = genGetInsForOper(oper, targetType);
    emitAttr    attr       = EA_ATTR(genTypeSize(targetType));

    GenTree* shiftBy = data->gtOp.gtOp2;
    if (shiftBy->isContainedIntOrIImmed())
    {
        int shiftByValue = (int)shiftBy->AsIntConCommon()->IconValue();
        ins              = genMapShiftInsToShiftByConstantIns(ins, shiftByValue);
        if (shiftByValue == 1)
        {
            // There is no source in this case, as the shift by count is embedded in the instruction opcode itself.
            getEmitter()->emitInsRMW(ins, attr, storeInd);
        }
        else
        {
            getEmitter()->emitInsRMW(ins, attr, storeInd, shiftBy);
        }
    }
    else
    {
        // We must have the number of bits to shift stored in ECX, since we constrained this node to
        // sit in ECX. In case this didn't happen, LSRA expects the code generator to move it since it's a single
        // register destination requirement.
        regNumber shiftReg = shiftBy->gtRegNum;
        genCopyRegIfNeeded(shiftBy, REG_RCX);

        // The shiftBy operand is implicit, so call the unary version of emitInsRMW.
        getEmitter()->emitInsRMW(ins, attr, storeInd);
    }
}

void CodeGen::genRegCopy(GenTree* treeNode)
{
    assert(treeNode->OperGet() == GT_COPY);
    GenTree* op1 = treeNode->gtOp.gtOp1;

    if (op1->IsMultiRegCall())
    {
        genConsumeReg(op1);

        GenTreeCopyOrReload* copyTree    = treeNode->AsCopyOrReload();
        GenTreeCall*         call        = op1->AsCall();
        ReturnTypeDesc*      retTypeDesc = call->GetReturnTypeDesc();
        unsigned             regCount    = retTypeDesc->GetReturnRegCount();

        for (unsigned i = 0; i < regCount; ++i)
        {
            var_types type    = retTypeDesc->GetReturnRegType(i);
            regNumber fromReg = call->GetRegNumByIdx(i);
            regNumber toReg   = copyTree->GetRegNumByIdx(i);

            // A Multi-reg GT_COPY node will have valid reg only for those
            // positions that corresponding result reg of call node needs
            // to be copied.
            if (toReg != REG_NA)
            {
                assert(toReg != fromReg);
                inst_RV_RV(ins_Copy(type), toReg, fromReg, type);
            }
        }
    }
    else
    {
        var_types targetType = treeNode->TypeGet();
        regNumber targetReg  = treeNode->gtRegNum;
        assert(targetReg != REG_NA);

        // Check whether this node and the node from which we're copying the value have
        // different register types. This can happen if (currently iff) we have a SIMD
        // vector type that fits in an integer register, in which case it is passed as
        // an argument, or returned from a call, in an integer register and must be
        // copied if it's in an xmm register.

        bool srcFltReg = (varTypeIsFloating(op1) || varTypeIsSIMD(op1));
        bool tgtFltReg = (varTypeIsFloating(treeNode) || varTypeIsSIMD(treeNode));
        if (srcFltReg != tgtFltReg)
        {
            instruction ins;
            regNumber   fpReg;
            regNumber   intReg;
            if (tgtFltReg)
            {
                ins    = ins_CopyIntToFloat(op1->TypeGet(), treeNode->TypeGet());
                fpReg  = targetReg;
                intReg = op1->gtRegNum;
            }
            else
            {
                ins    = ins_CopyFloatToInt(op1->TypeGet(), treeNode->TypeGet());
                intReg = targetReg;
                fpReg  = op1->gtRegNum;
            }
            inst_RV_RV(ins, fpReg, intReg, targetType);
        }
        else
        {
            inst_RV_RV(ins_Copy(targetType), targetReg, genConsumeReg(op1), targetType);
        }

        if (op1->IsLocal())
        {
            // The lclVar will never be a def.
            // If it is a last use, the lclVar will be killed by genConsumeReg(), as usual, and genProduceReg will
            // appropriately set the gcInfo for the copied value.
            // If not, there are two cases we need to handle:
            // - If this is a TEMPORARY copy (indicated by the GTF_VAR_DEATH flag) the variable
            //   will remain live in its original register.
            //   genProduceReg() will appropriately set the gcInfo for the copied value,
            //   and genConsumeReg will reset it.
            // - Otherwise, we need to update register info for the lclVar.

            GenTreeLclVarCommon* lcl = op1->AsLclVarCommon();
            assert((lcl->gtFlags & GTF_VAR_DEF) == 0);

            if ((lcl->gtFlags & GTF_VAR_DEATH) == 0 && (treeNode->gtFlags & GTF_VAR_DEATH) == 0)
            {
                LclVarDsc* varDsc = &compiler->lvaTable[lcl->gtLclNum];

                // If we didn't just spill it (in genConsumeReg, above), then update the register info
                if (varDsc->lvRegNum != REG_STK)
                {
                    // The old location is dying
                    genUpdateRegLife(varDsc, /*isBorn*/ false, /*isDying*/ true DEBUGARG(op1));

                    gcInfo.gcMarkRegSetNpt(genRegMask(op1->gtRegNum));

                    genUpdateVarReg(varDsc, treeNode);

                    // The new location is going live
                    genUpdateRegLife(varDsc, /*isBorn*/ true, /*isDying*/ false DEBUGARG(treeNode));
                }
            }
        }
    }

    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genStoreInd: Generate code for a GT_STOREIND node.
//
// Arguments:
//    treeNode - The GT_STOREIND node for which to generate code.
//
// Return Value:
//    none

void CodeGen::genStoreInd(GenTreePtr node)
{
    assert(node->OperGet() == GT_STOREIND);

#ifdef FEATURE_SIMD
    // Storing Vector3 of size 12 bytes through indirection
    if (node->TypeGet() == TYP_SIMD12)
    {
        genStoreIndTypeSIMD12(node);
        return;
    }
#endif // FEATURE_SIMD

    GenTreeStoreInd* storeInd   = node->AsStoreInd();
    GenTree*         data       = storeInd->Data();
    GenTree*         addr       = storeInd->Addr();
    var_types        targetType = storeInd->TypeGet();

    assert(!varTypeIsFloating(targetType) || (targetType == data->TypeGet()));

    GCInfo::WriteBarrierForm writeBarrierForm = gcInfo.gcIsWriteBarrierCandidate(storeInd, data);
    if (writeBarrierForm != GCInfo::WBF_NoBarrier)
    {
        // data and addr must be in registers.
        // Consume both registers so that any copies of interfering registers are taken care of.
        genConsumeOperands(storeInd->AsOp());

        if (genEmitOptimizedGCWriteBarrier(writeBarrierForm, addr, data))
        {
            return;
        }

        // At this point, we should not have any interference.
        // That is, 'data' must not be in REG_ARG_0, as that is where 'addr' must go.
        noway_assert(data->gtRegNum != REG_ARG_0);

        // addr goes in REG_ARG_0
        genCopyRegIfNeeded(addr, REG_ARG_0);

        // data goes in REG_ARG_1
        genCopyRegIfNeeded(data, REG_ARG_1);

        genGCWriteBarrier(storeInd, writeBarrierForm);
    }
    else
    {
        bool     reverseOps    = ((storeInd->gtFlags & GTF_REVERSE_OPS) != 0);
        bool     dataIsUnary   = false;
        bool     isRMWMemoryOp = storeInd->IsRMWMemoryOp();
        GenTree* rmwSrc        = nullptr;

        // We must consume the operands in the proper execution order, so that liveness is
        // updated appropriately.
        if (!reverseOps)
        {
            genConsumeAddress(addr);
        }

        // If storeInd represents a RMW memory op then its data is a non-leaf node marked as contained
        // and non-indir operand of data is the source of RMW memory op.
        if (isRMWMemoryOp)
        {
            assert(data->isContained() && !data->OperIsLeaf());

            GenTreePtr rmwDst = nullptr;

            dataIsUnary = (GenTree::OperIsUnary(data->OperGet()) != 0);
            if (!dataIsUnary)
            {
                if (storeInd->IsRMWDstOp1())
                {
                    rmwDst = data->gtGetOp1();
                    rmwSrc = data->gtGetOp2();
                }
                else
                {
                    assert(storeInd->IsRMWDstOp2());
                    rmwDst = data->gtGetOp2();
                    rmwSrc = data->gtGetOp1();
                }

                genConsumeRegs(rmwSrc);
            }
            else
            {
                // *(p) = oper *(p): Here addr = p, rmwsrc=rmwDst = *(p) i.e. GT_IND(p)
                // For unary RMW ops, src and dst of RMW memory op is the same.  Lower
                // clears operand counts on rmwSrc and we don't need to perform a
                // genConsumeReg() on it.
                assert(storeInd->IsRMWDstOp1());
                rmwSrc = data->gtGetOp1();
                rmwDst = data->gtGetOp1();
                assert(rmwSrc->isUsedFromMemory());
            }

            assert(rmwSrc != nullptr);
            assert(rmwDst != nullptr);
            assert(Lowering::IndirsAreEquivalent(rmwDst, storeInd));
        }
        else
        {
            genConsumeRegs(data);
        }

        if (reverseOps)
        {
            genConsumeAddress(addr);
        }

        if (isRMWMemoryOp)
        {
            if (dataIsUnary)
            {
                // generate code for unary RMW memory ops like neg/not
                getEmitter()->emitInsRMW(genGetInsForOper(data->OperGet(), data->TypeGet()), emitTypeSize(storeInd),
                                         storeInd);
            }
            else
            {
                if (data->OperIsShiftOrRotate())
                {
                    // Generate code for shift RMW memory ops.
                    // The data address needs to be op1 (it must be [addr] = [addr] <shift> <amount>, not [addr] =
                    // <amount> <shift> [addr]).
                    assert(storeInd->IsRMWDstOp1());
                    assert(rmwSrc == data->gtGetOp2());
                    genCodeForShiftRMW(storeInd);
                }
                else if (data->OperGet() == GT_ADD && (rmwSrc->IsIntegralConst(1) || rmwSrc->IsIntegralConst(-1)))
                {
                    // Generate "inc/dec [mem]" instead of "add/sub [mem], 1".
                    //
                    // Notes:
                    //  1) Global morph transforms GT_SUB(x, +/-1) into GT_ADD(x, -/+1).
                    //  2) TODO-AMD64: Debugger routine NativeWalker::Decode() runs into
                    //     an assert while decoding ModR/M byte of "inc dword ptr [rax]".
                    //     It is not clear whether Decode() can handle all possible
                    //     addr modes with inc/dec.  For this reason, inc/dec [mem]
                    //     is not generated while generating debuggable code.  Update
                    //     the above if condition once Decode() routine is fixed.
                    assert(rmwSrc->isContainedIntOrIImmed());
                    instruction ins = rmwSrc->IsIntegralConst(1) ? INS_inc : INS_dec;
                    getEmitter()->emitInsRMW(ins, emitTypeSize(storeInd), storeInd);
                }
                else
                {
                    // generate code for remaining binary RMW memory ops like add/sub/and/or/xor
                    getEmitter()->emitInsRMW(genGetInsForOper(data->OperGet(), data->TypeGet()), emitTypeSize(storeInd),
                                             storeInd, rmwSrc);
                }
            }
        }
        else
        {
            getEmitter()->emitInsMov(ins_Store(data->TypeGet()), emitTypeSize(storeInd), storeInd);
        }
    }
}

//------------------------------------------------------------------------
// genEmitOptimizedGCWriteBarrier: Generate write barrier store using the optimized
// helper functions.
//
// Arguments:
//    writeBarrierForm - the write barrier form to use
//    addr - the address at which to do the store
//    data - the data to store
//
// Return Value:
//    true if an optimized write barrier form was used, false if not. If this
//    function returns false, the caller must emit a "standard" write barrier.

bool CodeGen::genEmitOptimizedGCWriteBarrier(GCInfo::WriteBarrierForm writeBarrierForm, GenTree* addr, GenTree* data)
{
    assert(writeBarrierForm != GCInfo::WBF_NoBarrier);

#if defined(_TARGET_X86_) && NOGC_WRITE_BARRIERS
    bool useOptimizedWriteBarriers = true;

#ifdef DEBUG
    useOptimizedWriteBarriers =
        (writeBarrierForm != GCInfo::WBF_NoBarrier_CheckNotHeapInDebug); // This one is always a call to a C++ method.
#endif

    if (!useOptimizedWriteBarriers)
    {
        return false;
    }

    const static int regToHelper[2][8] = {
        // If the target is known to be in managed memory
        {
            CORINFO_HELP_ASSIGN_REF_EAX, CORINFO_HELP_ASSIGN_REF_ECX, -1, CORINFO_HELP_ASSIGN_REF_EBX, -1,
            CORINFO_HELP_ASSIGN_REF_EBP, CORINFO_HELP_ASSIGN_REF_ESI, CORINFO_HELP_ASSIGN_REF_EDI,
        },

        // Don't know if the target is in managed memory
        {
            CORINFO_HELP_CHECKED_ASSIGN_REF_EAX, CORINFO_HELP_CHECKED_ASSIGN_REF_ECX, -1,
            CORINFO_HELP_CHECKED_ASSIGN_REF_EBX, -1, CORINFO_HELP_CHECKED_ASSIGN_REF_EBP,
            CORINFO_HELP_CHECKED_ASSIGN_REF_ESI, CORINFO_HELP_CHECKED_ASSIGN_REF_EDI,
        },
    };

    noway_assert(regToHelper[0][REG_EAX] == CORINFO_HELP_ASSIGN_REF_EAX);
    noway_assert(regToHelper[0][REG_ECX] == CORINFO_HELP_ASSIGN_REF_ECX);
    noway_assert(regToHelper[0][REG_EBX] == CORINFO_HELP_ASSIGN_REF_EBX);
    noway_assert(regToHelper[0][REG_ESP] == -1);
    noway_assert(regToHelper[0][REG_EBP] == CORINFO_HELP_ASSIGN_REF_EBP);
    noway_assert(regToHelper[0][REG_ESI] == CORINFO_HELP_ASSIGN_REF_ESI);
    noway_assert(regToHelper[0][REG_EDI] == CORINFO_HELP_ASSIGN_REF_EDI);

    noway_assert(regToHelper[1][REG_EAX] == CORINFO_HELP_CHECKED_ASSIGN_REF_EAX);
    noway_assert(regToHelper[1][REG_ECX] == CORINFO_HELP_CHECKED_ASSIGN_REF_ECX);
    noway_assert(regToHelper[1][REG_EBX] == CORINFO_HELP_CHECKED_ASSIGN_REF_EBX);
    noway_assert(regToHelper[1][REG_ESP] == -1);
    noway_assert(regToHelper[1][REG_EBP] == CORINFO_HELP_CHECKED_ASSIGN_REF_EBP);
    noway_assert(regToHelper[1][REG_ESI] == CORINFO_HELP_CHECKED_ASSIGN_REF_ESI);
    noway_assert(regToHelper[1][REG_EDI] == CORINFO_HELP_CHECKED_ASSIGN_REF_EDI);

    regNumber reg = data->gtRegNum;
    noway_assert((reg != REG_ESP) && (reg != REG_WRITE_BARRIER));

    // Generate the following code:
    //            lea     edx, addr
    //            call    write_barrier_helper_reg

    // addr goes in REG_ARG_0
    genCopyRegIfNeeded(addr, REG_WRITE_BARRIER);

    unsigned tgtAnywhere = 0;
    if (writeBarrierForm != GCInfo::WBF_BarrierUnchecked)
    {
        tgtAnywhere = 1;
    }

    // We might want to call a modified version of genGCWriteBarrier() to get the benefit of
    // the FEATURE_COUNT_GC_WRITE_BARRIERS code there, but that code doesn't look like it works
    // with rationalized RyuJIT IR. So, for now, just emit the helper call directly here.

    genEmitHelperCall(regToHelper[tgtAnywhere][reg],
                      0,           // argSize
                      EA_PTRSIZE); // retSize

    return true;
#else  // !defined(_TARGET_X86_) || !NOGC_WRITE_BARRIERS
    return false;
#endif // !defined(_TARGET_X86_) || !NOGC_WRITE_BARRIERS
}

// Produce code for a GT_CALL node
void CodeGen::genCallInstruction(GenTreeCall* call)
{
    genAlignStackBeforeCall(call);

    gtCallTypes callType = (gtCallTypes)call->gtCallType;

    IL_OFFSETX ilOffset = BAD_IL_OFFSET;

    // all virtuals should have been expanded into a control expression
    assert(!call->IsVirtual() || call->gtControlExpr || call->gtCallAddr);

    // Insert a GS check if necessary
    if (call->IsTailCallViaHelper())
    {
        if (compiler->getNeedsGSSecurityCookie())
        {
#if FEATURE_FIXED_OUT_ARGS
            // If either of the conditions below is true, we will need a temporary register in order to perform the GS
            // cookie check. When FEATURE_FIXED_OUT_ARGS is disabled, we save and restore the temporary register using
            // push/pop. When FEATURE_FIXED_OUT_ARGS is enabled, however, we need an alternative solution. For now,
            // though, the tail prefix is ignored on all platforms that use fixed out args, so we should never hit this
            // case.
            assert(compiler->gsGlobalSecurityCookieAddr == nullptr);
            assert((int)compiler->gsGlobalSecurityCookieVal == (ssize_t)compiler->gsGlobalSecurityCookieVal);
#endif
            genEmitGSCookieCheck(true);
        }
    }

    // Consume all the arg regs
    for (GenTreePtr list = call->gtCallLateArgs; list; list = list->MoveNext())
    {
        assert(list->OperIsList());

        GenTreePtr argNode = list->Current();

        fgArgTabEntryPtr curArgTabEntry = compiler->gtArgEntryByNode(call, argNode->gtSkipReloadOrCopy());
        assert(curArgTabEntry);

        if (curArgTabEntry->regNum == REG_STK)
        {
            continue;
        }

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING
        // Deal with multi register passed struct args.
        if (argNode->OperGet() == GT_FIELD_LIST)
        {
            GenTreeFieldList* fieldListPtr = argNode->AsFieldList();
            unsigned          iterationNum = 0;
            for (; fieldListPtr != nullptr; fieldListPtr = fieldListPtr->Rest(), iterationNum++)
            {
                GenTreePtr putArgRegNode = fieldListPtr->gtOp.gtOp1;
                assert(putArgRegNode->gtOper == GT_PUTARG_REG);
                regNumber argReg = REG_NA;

                if (iterationNum == 0)
                {
                    argReg = curArgTabEntry->regNum;
                }
                else
                {
                    assert(iterationNum == 1);
                    argReg = curArgTabEntry->otherRegNum;
                }

                genConsumeReg(putArgRegNode);

                // Validate the putArgRegNode has the right type.
                assert(putArgRegNode->TypeGet() ==
                       compiler->GetTypeFromClassificationAndSizes(curArgTabEntry->structDesc
                                                                       .eightByteClassifications[iterationNum],
                                                                   curArgTabEntry->structDesc
                                                                       .eightByteSizes[iterationNum]));
                if (putArgRegNode->gtRegNum != argReg)
                {
                    inst_RV_RV(ins_Move_Extend(putArgRegNode->TypeGet(), putArgRegNode->InReg()), argReg,
                               putArgRegNode->gtRegNum);
                }
            }
        }
        else
#endif // FEATURE_UNIX_AMD64_STRUCT_PASSING
        {
            regNumber argReg = curArgTabEntry->regNum;
            genConsumeReg(argNode);
            if (argNode->gtRegNum != argReg)
            {
                inst_RV_RV(ins_Move_Extend(argNode->TypeGet(), argNode->InReg()), argReg, argNode->gtRegNum);
            }
        }

#if FEATURE_VARARG
        // In the case of a varargs call,
        // the ABI dictates that if we have floating point args,
        // we must pass the enregistered arguments in both the
        // integer and floating point registers so, let's do that.
        if (call->IsVarargs() && varTypeIsFloating(argNode))
        {
            regNumber   targetReg = compiler->getCallArgIntRegister(argNode->gtRegNum);
            instruction ins       = ins_CopyFloatToInt(argNode->TypeGet(), TYP_LONG);
            inst_RV_RV(ins, argNode->gtRegNum, targetReg);
        }
#endif // FEATURE_VARARG
    }

#if defined(_TARGET_X86_) || defined(FEATURE_UNIX_AMD64_STRUCT_PASSING)
    // The call will pop its arguments.
    // for each putarg_stk:
    ssize_t    stackArgBytes = 0;
    GenTreePtr args          = call->gtCallArgs;
    while (args)
    {
        GenTreePtr arg = args->gtOp.gtOp1;
        if (arg->OperGet() != GT_ARGPLACE && !(arg->gtFlags & GTF_LATE_ARG))
        {
#if defined(_TARGET_X86_)
            if ((arg->OperGet() == GT_PUTARG_STK) && (arg->gtGetOp1()->OperGet() == GT_FIELD_LIST))
            {
                fgArgTabEntryPtr curArgTabEntry = compiler->gtArgEntryByNode(call, arg);
                assert(curArgTabEntry);
                stackArgBytes += curArgTabEntry->numSlots * TARGET_POINTER_SIZE;
            }
            else
#endif // defined(_TARGET_X86_)

#ifdef FEATURE_PUT_STRUCT_ARG_STK
                if (genActualType(arg->TypeGet()) == TYP_STRUCT)
            {
                assert(arg->OperGet() == GT_PUTARG_STK);

                GenTreeObj* obj      = arg->gtGetOp1()->AsObj();
                unsigned    argBytes = (unsigned)roundUp(obj->gtBlkSize, TARGET_POINTER_SIZE);
#ifdef DEBUG
                fgArgTabEntryPtr curArgTabEntry = compiler->gtArgEntryByNode(call, arg);
                assert((curArgTabEntry->numSlots * TARGET_POINTER_SIZE) == argBytes);
#endif // DEBUG
                stackArgBytes += argBytes;
            }
            else
#endif // FEATURE_PUT_STRUCT_ARG_STK

            {
                stackArgBytes += genTypeSize(genActualType(arg->TypeGet()));
            }
        }
        args = args->gtOp.gtOp2;
    }
#endif // defined(_TARGET_X86_) || defined(FEATURE_UNIX_AMD64_STRUCT_PASSING)

    // Insert a null check on "this" pointer if asked.
    if (call->NeedsNullCheck())
    {
        const regNumber regThis = genGetThisArgReg(call);
        getEmitter()->emitIns_AR_R(INS_cmp, EA_4BYTE, regThis, regThis, 0);
    }

    // Either gtControlExpr != null or gtCallAddr != null or it is a direct non-virtual call to a user or helper method.
    CORINFO_METHOD_HANDLE methHnd;
    GenTree*              target = call->gtControlExpr;
    if (callType == CT_INDIRECT)
    {
        assert(target == nullptr);
        target  = call->gtCallAddr;
        methHnd = nullptr;
    }
    else
    {
        methHnd = call->gtCallMethHnd;
    }

    CORINFO_SIG_INFO* sigInfo = nullptr;
#ifdef DEBUG
    // Pass the call signature information down into the emitter so the emitter can associate
    // native call sites with the signatures they were generated from.
    if (callType != CT_HELPER)
    {
        sigInfo = call->callSig;
    }
#endif // DEBUG

    // If fast tail call, then we are done.  In this case we setup the args (both reg args
    // and stack args in incoming arg area) and call target in rax.  Epilog sequence would
    // generate "jmp rax".
    if (call->IsFastTailCall())
    {
        // Don't support fast tail calling JIT helpers
        assert(callType != CT_HELPER);

        // Fast tail calls materialize call target either in gtControlExpr or in gtCallAddr.
        assert(target != nullptr);

        genConsumeReg(target);
        genCopyRegIfNeeded(target, REG_RAX);
        return;
    }

    // For a pinvoke to unmanged code we emit a label to clear
    // the GC pointer state before the callsite.
    // We can't utilize the typical lazy killing of GC pointers
    // at (or inside) the callsite.
    if (call->IsUnmanaged())
    {
        genDefineTempLabel(genCreateTempLabel());
    }

    // Determine return value size(s).
    ReturnTypeDesc* retTypeDesc   = call->GetReturnTypeDesc();
    emitAttr        retSize       = EA_PTRSIZE;
    emitAttr        secondRetSize = EA_UNKNOWN;

    if (call->HasMultiRegRetVal())
    {
        retSize       = emitTypeSize(retTypeDesc->GetReturnRegType(0));
        secondRetSize = emitTypeSize(retTypeDesc->GetReturnRegType(1));
    }
    else
    {
        assert(!varTypeIsStruct(call));

        if (call->gtType == TYP_REF || call->gtType == TYP_ARRAY)
        {
            retSize = EA_GCREF;
        }
        else if (call->gtType == TYP_BYREF)
        {
            retSize = EA_BYREF;
        }
    }

    bool            fPossibleSyncHelperCall = false;
    CorInfoHelpFunc helperNum               = CORINFO_HELP_UNDEF;

    // We need to propagate the IL offset information to the call instruction, so we can emit
    // an IL to native mapping record for the call, to support managed return value debugging.
    // We don't want tail call helper calls that were converted from normal calls to get a record,
    // so we skip this hash table lookup logic in that case.
    if (compiler->opts.compDbgInfo && compiler->genCallSite2ILOffsetMap != nullptr && !call->IsTailCall())
    {
        (void)compiler->genCallSite2ILOffsetMap->Lookup(call, &ilOffset);
    }

#if defined(_TARGET_X86_)
    bool fCallerPop = (call->gtFlags & GTF_CALL_POP_ARGS) != 0;

#ifdef UNIX_X86_ABI
    {
        CorInfoCallConv callConv = CORINFO_CALLCONV_DEFAULT;

        if ((callType != CT_HELPER) && call->callSig)
        {
            callConv = call->callSig->callConv;
        }

        fCallerPop |= IsCallerPop(callConv);
    }
#endif // UNIX_X86_ABI

    // If the callee pops the arguments, we pass a positive value as the argSize, and the emitter will
    // adjust its stack level accordingly.
    // If the caller needs to explicitly pop its arguments, we must pass a negative value, and then do the
    // pop when we're done.
    ssize_t argSizeForEmitter = stackArgBytes;
    if (fCallerPop)
    {
        argSizeForEmitter = -stackArgBytes;
    }
#endif // defined(_TARGET_X86_)

#ifdef FEATURE_AVX_SUPPORT
    // When it's a PInvoke call and the call type is USER function, we issue VZEROUPPER here
    // if the function contains 256bit AVX instructions, this is to avoid AVX-256 to Legacy SSE
    // transition penalty, assuming the user function contains legacy SSE instruction.
    // To limit code size increase impact: we only issue VZEROUPPER before PInvoke call, not issue
    // VZEROUPPER after PInvoke call because transition penalty from legacy SSE to AVX only happens
    // when there's preceding 256-bit AVX to legacy SSE transition penalty.
    if (call->IsPInvoke() && (call->gtCallType == CT_USER_FUNC) && getEmitter()->Contains256bitAVX())
    {
        assert(compiler->getSIMDInstructionSet() == InstructionSet_AVX);
        instGen(INS_vzeroupper);
    }
#endif

    if (target != nullptr)
    {
#ifdef _TARGET_X86_
        if (call->IsVirtualStub() && (call->gtCallType == CT_INDIRECT))
        {
            // On x86, we need to generate a very specific pattern for indirect VSD calls:
            //
            //    3-byte nop
            //    call dword ptr [eax]
            //
            // Where EAX is also used as an argument to the stub dispatch helper. Make
            // sure that the call target address is computed into EAX in this case.

            assert(REG_VIRTUAL_STUB_PARAM == REG_VIRTUAL_STUB_TARGET);

            assert(target->isContainedIndir());
            assert(target->OperGet() == GT_IND);

            GenTree* addr = target->AsIndir()->Addr();
            assert(addr->isUsedFromReg());

            genConsumeReg(addr);
            genCopyRegIfNeeded(addr, REG_VIRTUAL_STUB_TARGET);

            getEmitter()->emitIns_Nop(3);

            // clang-format off
            getEmitter()->emitIns_Call(emitter::EmitCallType(emitter::EC_INDIR_ARD),
                                       methHnd,
                                       INDEBUG_LDISASM_COMMA(sigInfo)
                                       nullptr,
                                       argSizeForEmitter,
                                       retSize
                                       MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                                       gcInfo.gcVarPtrSetCur,
                                       gcInfo.gcRegGCrefSetCur,
                                       gcInfo.gcRegByrefSetCur,
                                       ilOffset, REG_VIRTUAL_STUB_TARGET, REG_NA, 1, 0);
            // clang-format on
        }
        else
#endif
            if (target->isContainedIndir())
        {
            if (target->AsIndir()->HasBase() && target->AsIndir()->Base()->isContainedIntOrIImmed())
            {
                // Note that if gtControlExpr is an indir of an absolute address, we mark it as
                // contained only if it can be encoded as PC-relative offset.
                assert(target->AsIndir()->Base()->AsIntConCommon()->FitsInAddrBase(compiler));

                // clang-format off
                genEmitCall(emitter::EC_FUNC_TOKEN_INDIR,
                            methHnd,
                            INDEBUG_LDISASM_COMMA(sigInfo)
                            (void*) target->AsIndir()->Base()->AsIntConCommon()->IconValue()
                            X86_ARG(argSizeForEmitter),
                            retSize
                            MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                            ilOffset);
                // clang-format on
            }
            else
            {
                // clang-format off
                genEmitCall(emitter::EC_INDIR_ARD,
                            methHnd,
                            INDEBUG_LDISASM_COMMA(sigInfo)
                            target->AsIndir()
                            X86_ARG(argSizeForEmitter),
                            retSize
                            MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                            ilOffset);
                // clang-format on
            }
        }
        else
        {
            // We have already generated code for gtControlExpr evaluating it into a register.
            // We just need to emit "call reg" in this case.
            assert(genIsValidIntReg(target->gtRegNum));

            // clang-format off
            genEmitCall(emitter::EC_INDIR_R,
                        methHnd,
                        INDEBUG_LDISASM_COMMA(sigInfo)
                        nullptr // addr
                        X86_ARG(argSizeForEmitter),
                        retSize
                        MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                        ilOffset,
                        genConsumeReg(target));
            // clang-format on
        }
    }
#ifdef FEATURE_READYTORUN_COMPILER
    else if (call->gtEntryPoint.addr != nullptr)
    {
        // clang-format off
        genEmitCall((call->gtEntryPoint.accessType == IAT_VALUE) ? emitter::EC_FUNC_TOKEN
                                                                 : emitter::EC_FUNC_TOKEN_INDIR,
                    methHnd,
                    INDEBUG_LDISASM_COMMA(sigInfo)
                    (void*) call->gtEntryPoint.addr
                    X86_ARG(argSizeForEmitter),
                    retSize
                    MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                    ilOffset);
        // clang-format on
    }
#endif
    else
    {
        // Generate a direct call to a non-virtual user defined or helper method
        assert(callType == CT_HELPER || callType == CT_USER_FUNC);

        void* addr = nullptr;
        if (callType == CT_HELPER)
        {
            // Direct call to a helper method.
            helperNum = compiler->eeGetHelperNum(methHnd);
            noway_assert(helperNum != CORINFO_HELP_UNDEF);

            void* pAddr = nullptr;
            addr        = compiler->compGetHelperFtn(helperNum, (void**)&pAddr);

            if (addr == nullptr)
            {
                addr = pAddr;
            }

            // tracking of region protected by the monitor in synchronized methods
            if (compiler->info.compFlags & CORINFO_FLG_SYNCH)
            {
                fPossibleSyncHelperCall = true;
            }
        }
        else
        {
            // Direct call to a non-virtual user function.
            addr = call->gtDirectCallAddress;
        }

        // Non-virtual direct calls to known addresses

        // clang-format off
        genEmitCall(emitter::EC_FUNC_TOKEN,
                    methHnd,
                    INDEBUG_LDISASM_COMMA(sigInfo)
                    addr
                    X86_ARG(argSizeForEmitter),
                    retSize
                    MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(secondRetSize),
                    ilOffset);
        // clang-format on
    }

    // if it was a pinvoke we may have needed to get the address of a label
    if (genPendingCallLabel)
    {
        assert(call->IsUnmanaged());
        genDefineTempLabel(genPendingCallLabel);
        genPendingCallLabel = nullptr;
    }

    // Update GC info:
    // All Callee arg registers are trashed and no longer contain any GC pointers.
    // TODO-XArch-Bug?: As a matter of fact shouldn't we be killing all of callee trashed regs here?
    // For now we will assert that other than arg regs gc ref/byref set doesn't contain any other
    // registers from RBM_CALLEE_TRASH.
    assert((gcInfo.gcRegGCrefSetCur & (RBM_CALLEE_TRASH & ~RBM_ARG_REGS)) == 0);
    assert((gcInfo.gcRegByrefSetCur & (RBM_CALLEE_TRASH & ~RBM_ARG_REGS)) == 0);
    gcInfo.gcRegGCrefSetCur &= ~RBM_ARG_REGS;
    gcInfo.gcRegByrefSetCur &= ~RBM_ARG_REGS;

    var_types returnType = call->TypeGet();
    if (returnType != TYP_VOID)
    {
#ifdef _TARGET_X86_
        if (varTypeIsFloating(returnType))
        {
            // Spill the value from the fp stack.
            // Then, load it into the target register.
            call->gtFlags |= GTF_SPILL;
            regSet.rsSpillFPStack(call);
            call->gtFlags |= GTF_SPILLED;
            call->gtFlags &= ~GTF_SPILL;
        }
        else
#endif // _TARGET_X86_
        {
            regNumber returnReg;

            if (call->HasMultiRegRetVal())
            {
                assert(retTypeDesc != nullptr);
                unsigned regCount = retTypeDesc->GetReturnRegCount();

                // If regs allocated to call node are different from ABI return
                // regs in which the call has returned its result, move the result
                // to regs allocated to call node.
                for (unsigned i = 0; i < regCount; ++i)
                {
                    var_types regType      = retTypeDesc->GetReturnRegType(i);
                    returnReg              = retTypeDesc->GetABIReturnReg(i);
                    regNumber allocatedReg = call->GetRegNumByIdx(i);
                    if (returnReg != allocatedReg)
                    {
                        inst_RV_RV(ins_Copy(regType), allocatedReg, returnReg, regType);
                    }
                }

#ifdef FEATURE_SIMD
                // A Vector3 return value is stored in xmm0 and xmm1.
                // RyuJIT assumes that the upper unused bits of xmm1 are cleared but
                // the native compiler doesn't guarantee it.
                if (returnType == TYP_SIMD12)
                {
                    returnReg = retTypeDesc->GetABIReturnReg(1);
                    // Clear the upper 32 bits by two shift instructions.
                    // retReg = retReg << 96
                    // retReg = retReg >> 96
                    getEmitter()->emitIns_R_I(INS_pslldq, emitActualTypeSize(TYP_SIMD12), returnReg, 12);
                    getEmitter()->emitIns_R_I(INS_psrldq, emitActualTypeSize(TYP_SIMD12), returnReg, 12);
                }
#endif // FEATURE_SIMD
            }
            else
            {
#ifdef _TARGET_X86_
                if (call->IsHelperCall(compiler, CORINFO_HELP_INIT_PINVOKE_FRAME))
                {
                    // The x86 CORINFO_HELP_INIT_PINVOKE_FRAME helper uses a custom calling convention that returns with
                    // TCB in REG_PINVOKE_TCB. AMD64/ARM64 use the standard calling convention. fgMorphCall() sets the
                    // correct argument registers.
                    returnReg = REG_PINVOKE_TCB;
                }
                else
#endif // _TARGET_X86_
                    if (varTypeIsFloating(returnType))
                {
                    returnReg = REG_FLOATRET;
                }
                else
                {
                    returnReg = REG_INTRET;
                }

                if (call->gtRegNum != returnReg)
                {
                    inst_RV_RV(ins_Copy(returnType), call->gtRegNum, returnReg, returnType);
                }
            }

            genProduceReg(call);
        }
    }

    // If there is nothing next, that means the result is thrown away, so this value is not live.
    // However, for minopts or debuggable code, we keep it live to support managed return value debugging.
    if ((call->gtNext == nullptr) && !compiler->opts.MinOpts() && !compiler->opts.compDbgCode)
    {
        gcInfo.gcMarkRegSetNpt(RBM_INTRET);
    }

#if !FEATURE_EH_FUNCLETS
    //-------------------------------------------------------------------------
    // Create a label for tracking of region protected by the monitor in synchronized methods.
    // This needs to be here, rather than above where fPossibleSyncHelperCall is set,
    // so the GC state vars have been updated before creating the label.

    if (fPossibleSyncHelperCall)
    {
        switch (helperNum)
        {
            case CORINFO_HELP_MON_ENTER:
            case CORINFO_HELP_MON_ENTER_STATIC:
                noway_assert(compiler->syncStartEmitCookie == NULL);
                compiler->syncStartEmitCookie =
                    getEmitter()->emitAddLabel(gcInfo.gcVarPtrSetCur, gcInfo.gcRegGCrefSetCur, gcInfo.gcRegByrefSetCur);
                noway_assert(compiler->syncStartEmitCookie != NULL);
                break;
            case CORINFO_HELP_MON_EXIT:
            case CORINFO_HELP_MON_EXIT_STATIC:
                noway_assert(compiler->syncEndEmitCookie == NULL);
                compiler->syncEndEmitCookie =
                    getEmitter()->emitAddLabel(gcInfo.gcVarPtrSetCur, gcInfo.gcRegGCrefSetCur, gcInfo.gcRegByrefSetCur);
                noway_assert(compiler->syncEndEmitCookie != NULL);
                break;
            default:
                break;
        }
    }
#endif // !FEATURE_EH_FUNCLETS

    unsigned stackAdjustBias = 0;

#if defined(_TARGET_X86_)
    // Is the caller supposed to pop the arguments?
    if (fCallerPop && (stackArgBytes != 0))
    {
        stackAdjustBias = stackArgBytes;
    }

    SubtractStackLevel(stackArgBytes);
#endif // _TARGET_X86_

    genRemoveAlignmentAfterCall(call, stackAdjustBias);
}

// Produce code for a GT_JMP node.
// The arguments of the caller needs to be transferred to the callee before exiting caller.
// The actual jump to callee is generated as part of caller epilog sequence.
// Therefore the codegen of GT_JMP is to ensure that the callee arguments are correctly setup.
void CodeGen::genJmpMethod(GenTreePtr jmp)
{
    assert(jmp->OperGet() == GT_JMP);
    assert(compiler->compJmpOpUsed);

    // If no arguments, nothing to do
    if (compiler->info.compArgsCount == 0)
    {
        return;
    }

    // Make sure register arguments are in their initial registers
    // and stack arguments are put back as well.
    unsigned   varNum;
    LclVarDsc* varDsc;

    // First move any en-registered stack arguments back to the stack.
    // At the same time any reg arg not in correct reg is moved back to its stack location.
    //
    // We are not strictly required to spill reg args that are not in the desired reg for a jmp call
    // But that would require us to deal with circularity while moving values around.  Spilling
    // to stack makes the implementation simple, which is not a bad trade off given Jmp calls
    // are not frequent.
    for (varNum = 0; (varNum < compiler->info.compArgsCount); varNum++)
    {
        varDsc = compiler->lvaTable + varNum;

        if (varDsc->lvPromoted)
        {
            noway_assert(varDsc->lvFieldCnt == 1); // We only handle one field here

            unsigned fieldVarNum = varDsc->lvFieldLclStart;
            varDsc               = compiler->lvaTable + fieldVarNum;
        }
        noway_assert(varDsc->lvIsParam);

        if (varDsc->lvIsRegArg && (varDsc->lvRegNum != REG_STK))
        {
            // Skip reg args which are already in its right register for jmp call.
            // If not, we will spill such args to their stack locations.
            //
            // If we need to generate a tail call profiler hook, then spill all
            // arg regs to free them up for the callback.
            if (!compiler->compIsProfilerHookNeeded() && (varDsc->lvRegNum == varDsc->lvArgReg))
            {
                continue;
            }
        }
        else if (varDsc->lvRegNum == REG_STK)
        {
            // Skip args which are currently living in stack.
            continue;
        }

        // If we came here it means either a reg argument not in the right register or
        // a stack argument currently living in a register.  In either case the following
        // assert should hold.
        assert(varDsc->lvRegNum != REG_STK);

        var_types loadType = varDsc->lvaArgType();
        getEmitter()->emitIns_S_R(ins_Store(loadType), emitTypeSize(loadType), varDsc->lvRegNum, varNum, 0);

        // Update lvRegNum life and GC info to indicate lvRegNum is dead and varDsc stack slot is going live.
        // Note that we cannot modify varDsc->lvRegNum here because another basic block may not be expecting it.
        // Therefore manually update life of varDsc->lvRegNum.
        regMaskTP tempMask = varDsc->lvRegMask();
        regSet.RemoveMaskVars(tempMask);
        gcInfo.gcMarkRegSetNpt(tempMask);
        if (compiler->lvaIsGCTracked(varDsc))
        {
#ifdef DEBUG
            if (!VarSetOps::IsMember(compiler, gcInfo.gcVarPtrSetCur, varDsc->lvVarIndex))
            {
                JITDUMP("\t\t\t\t\t\t\tVar V%02u becoming live\n", varNum);
            }
            else
            {
                JITDUMP("\t\t\t\t\t\t\tVar V%02u continuing live\n", varNum);
            }
#endif // DEBUG

            VarSetOps::AddElemD(compiler, gcInfo.gcVarPtrSetCur, varDsc->lvVarIndex);
        }
    }

#ifdef PROFILING_SUPPORTED
    // At this point all arg regs are free.
    // Emit tail call profiler callback.
    genProfilingLeaveCallback(CORINFO_HELP_PROF_FCN_TAILCALL);
#endif

    // Next move any un-enregistered register arguments back to their register.
    regMaskTP fixedIntArgMask = RBM_NONE;    // tracks the int arg regs occupying fixed args in case of a vararg method.
    unsigned  firstArgVarNum  = BAD_VAR_NUM; // varNum of the first argument in case of a vararg method.
    for (varNum = 0; (varNum < compiler->info.compArgsCount); varNum++)
    {
        varDsc = compiler->lvaTable + varNum;
        if (varDsc->lvPromoted)
        {
            noway_assert(varDsc->lvFieldCnt == 1); // We only handle one field here

            unsigned fieldVarNum = varDsc->lvFieldLclStart;
            varDsc               = compiler->lvaTable + fieldVarNum;
        }
        noway_assert(varDsc->lvIsParam);

        // Skip if arg not passed in a register.
        if (!varDsc->lvIsRegArg)
        {
            continue;
        }

#if defined(FEATURE_UNIX_AMD64_STRUCT_PASSING)
        if (varTypeIsStruct(varDsc))
        {
            CORINFO_CLASS_HANDLE typeHnd = varDsc->lvVerTypeInfo.GetClassHandle();
            assert(typeHnd != nullptr);

            SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc;
            compiler->eeGetSystemVAmd64PassStructInRegisterDescriptor(typeHnd, &structDesc);
            assert(structDesc.passedInRegisters);

            unsigned __int8 offset0 = 0;
            unsigned __int8 offset1 = 0;
            var_types       type0   = TYP_UNKNOWN;
            var_types       type1   = TYP_UNKNOWN;

            // Get the eightbyte data
            compiler->GetStructTypeOffset(structDesc, &type0, &type1, &offset0, &offset1);

            // Move the values into the right registers.
            //

            // Update varDsc->lvArgReg and lvOtherArgReg life and GC Info to indicate varDsc stack slot is dead and
            // argReg is going live. Note that we cannot modify varDsc->lvRegNum and lvOtherArgReg here because another
            // basic block may not be expecting it. Therefore manually update life of argReg.  Note that GT_JMP marks
            // the end of the basic block and after which reg life and gc info will be recomputed for the new block in
            // genCodeForBBList().
            if (type0 != TYP_UNKNOWN)
            {
                getEmitter()->emitIns_R_S(ins_Load(type0), emitTypeSize(type0), varDsc->lvArgReg, varNum, offset0);
                regSet.rsMaskVars |= genRegMask(varDsc->lvArgReg);
                gcInfo.gcMarkRegPtrVal(varDsc->lvArgReg, type0);
            }

            if (type1 != TYP_UNKNOWN)
            {
                getEmitter()->emitIns_R_S(ins_Load(type1), emitTypeSize(type1), varDsc->lvOtherArgReg, varNum, offset1);
                regSet.rsMaskVars |= genRegMask(varDsc->lvOtherArgReg);
                gcInfo.gcMarkRegPtrVal(varDsc->lvOtherArgReg, type1);
            }

            if (varDsc->lvTracked)
            {
                VarSetOps::RemoveElemD(compiler, gcInfo.gcVarPtrSetCur, varDsc->lvVarIndex);
            }
        }
        else
#endif // !defined(FEATURE_UNIX_AMD64_STRUCT_PASSING)
        {
            // Register argument
            noway_assert(isRegParamType(genActualType(varDsc->TypeGet())));

            // Is register argument already in the right register?
            // If not load it from its stack location.
            var_types loadType = varDsc->lvaArgType();
            regNumber argReg   = varDsc->lvArgReg; // incoming arg register

            if (varDsc->lvRegNum != argReg)
            {
                assert(genIsValidReg(argReg));
                getEmitter()->emitIns_R_S(ins_Load(loadType), emitTypeSize(loadType), argReg, varNum, 0);

                // Update argReg life and GC Info to indicate varDsc stack slot is dead and argReg is going live.
                // Note that we cannot modify varDsc->lvRegNum here because another basic block may not be expecting it.
                // Therefore manually update life of argReg.  Note that GT_JMP marks the end of the basic block
                // and after which reg life and gc info will be recomputed for the new block in genCodeForBBList().
                regSet.AddMaskVars(genRegMask(argReg));
                gcInfo.gcMarkRegPtrVal(argReg, loadType);
                if (compiler->lvaIsGCTracked(varDsc))
                {
#ifdef DEBUG
                    if (VarSetOps::IsMember(compiler, gcInfo.gcVarPtrSetCur, varDsc->lvVarIndex))
                    {
                        JITDUMP("\t\t\t\t\t\t\tVar V%02u becoming dead\n", varNum);
                    }
                    else
                    {
                        JITDUMP("\t\t\t\t\t\t\tVar V%02u continuing dead\n", varNum);
                    }
#endif // DEBUG

                    VarSetOps::RemoveElemD(compiler, gcInfo.gcVarPtrSetCur, varDsc->lvVarIndex);
                }
            }
        }

#if FEATURE_VARARG && defined(_TARGET_AMD64_)
        // In case of a jmp call to a vararg method also pass the float/double arg in the corresponding int arg
        // register. This is due to the AMD64 ABI which requires floating point values passed to varargs functions to
        // be passed in both integer and floating point registers. It doesn't apply to x86, which passes floating point
        // values on the stack.
        if (compiler->info.compIsVarArgs)
        {
            regNumber intArgReg;
            var_types loadType = varDsc->lvaArgType();
            regNumber argReg   = varDsc->lvArgReg; // incoming arg register

            if (varTypeIsFloating(loadType))
            {
                intArgReg       = compiler->getCallArgIntRegister(argReg);
                instruction ins = ins_CopyFloatToInt(loadType, TYP_LONG);
                inst_RV_RV(ins, argReg, intArgReg, loadType);
            }
            else
            {
                intArgReg = argReg;
            }

            fixedIntArgMask |= genRegMask(intArgReg);

            if (intArgReg == REG_ARG_0)
            {
                assert(firstArgVarNum == BAD_VAR_NUM);
                firstArgVarNum = varNum;
            }
        }
#endif // FEATURE_VARARG
    }

#if FEATURE_VARARG && defined(_TARGET_AMD64_)
    // Jmp call to a vararg method - if the method has fewer than 4 fixed arguments,
    // load the remaining arg registers (both int and float) from the corresponding
    // shadow stack slots.  This is for the reason that we don't know the number and type
    // of non-fixed params passed by the caller, therefore we have to assume the worst case
    // of caller passing float/double args both in int and float arg regs.
    //
    // This doesn't apply to x86, which doesn't pass floating point values in floating
    // point registers.
    //
    // The caller could have passed gc-ref/byref type var args.  Since these are var args
    // the callee no way of knowing their gc-ness.  Therefore, mark the region that loads
    // remaining arg registers from shadow stack slots as non-gc interruptible.
    if (fixedIntArgMask != RBM_NONE)
    {
        assert(compiler->info.compIsVarArgs);
        assert(firstArgVarNum != BAD_VAR_NUM);

        regMaskTP remainingIntArgMask = RBM_ARG_REGS & ~fixedIntArgMask;
        if (remainingIntArgMask != RBM_NONE)
        {
            instruction insCopyIntToFloat = ins_CopyIntToFloat(TYP_LONG, TYP_DOUBLE);
            getEmitter()->emitDisableGC();
            for (int argNum = 0, argOffset = 0; argNum < MAX_REG_ARG; ++argNum)
            {
                regNumber argReg     = intArgRegs[argNum];
                regMaskTP argRegMask = genRegMask(argReg);

                if ((remainingIntArgMask & argRegMask) != 0)
                {
                    remainingIntArgMask &= ~argRegMask;
                    getEmitter()->emitIns_R_S(INS_mov, EA_8BYTE, argReg, firstArgVarNum, argOffset);

                    // also load it in corresponding float arg reg
                    regNumber floatReg = compiler->getCallArgFloatRegister(argReg);
                    inst_RV_RV(insCopyIntToFloat, floatReg, argReg);
                }

                argOffset += REGSIZE_BYTES;
            }
            getEmitter()->emitEnableGC();
        }
    }
#endif // FEATURE_VARARG
}

// produce code for a GT_LEA subnode
void CodeGen::genLeaInstruction(GenTreeAddrMode* lea)
{
    emitAttr size = emitTypeSize(lea);
    genConsumeOperands(lea);

    if (lea->Base() && lea->Index())
    {
        regNumber baseReg  = lea->Base()->gtRegNum;
        regNumber indexReg = lea->Index()->gtRegNum;
        getEmitter()->emitIns_R_ARX(INS_lea, size, lea->gtRegNum, baseReg, indexReg, lea->gtScale, lea->gtOffset);
    }
    else if (lea->Base())
    {
        getEmitter()->emitIns_R_AR(INS_lea, size, lea->gtRegNum, lea->Base()->gtRegNum, lea->gtOffset);
    }
    else if (lea->Index())
    {
        getEmitter()->emitIns_R_ARX(INS_lea, size, lea->gtRegNum, REG_NA, lea->Index()->gtRegNum, lea->gtScale,
                                    lea->gtOffset);
    }

    genProduceReg(lea);
}

//-------------------------------------------------------------------------------------------
// genJumpKindsForTree:  Determine the number and kinds of conditional branches
//                       necessary to implement the given GT_CMP node
//
// Arguments:
//    cmpTree          - (input) The GenTree node that is used to set the Condition codes
//                     - The GenTree Relop node that was used to set the Condition codes
//   jmpKind[2]        - (output) One or two conditional branch instructions
//   jmpToTrueLabel[2] - (output) When true we branch to the true case
//                       When false we create a second label and branch to the false case
//                       Only GT_EQ for a floating point compares can have a false value.
//
// Return Value:
//    Sets the proper values into the array elements of jmpKind[] and jmpToTrueLabel[]
//
// Assumptions:
//    At least one conditional branch instruction will be returned.
//    Typically only one conditional branch is needed
//     and the second jmpKind[] value is set to EJ_NONE
//
// Notes:
//    jmpToTrueLabel[i]= true  implies branch when the compare operation is true.
//    jmpToTrueLabel[i]= false implies branch when the compare operation is false.
//-------------------------------------------------------------------------------------------

// static
void CodeGen::genJumpKindsForTree(GenTreePtr cmpTree, emitJumpKind jmpKind[2], bool jmpToTrueLabel[2])
{
    // Except for BEQ (=  ordered GT_EQ) both jumps are to the true label.
    jmpToTrueLabel[0] = true;
    jmpToTrueLabel[1] = true;

    // For integer comparisons just use genJumpKindForOper
    if (!varTypeIsFloating(cmpTree->gtOp.gtOp1->gtEffectiveVal()))
    {
        CompareKind compareKind = ((cmpTree->gtFlags & GTF_UNSIGNED) != 0) ? CK_UNSIGNED : CK_SIGNED;
        jmpKind[0]              = genJumpKindForOper(cmpTree->gtOper, compareKind);
        jmpKind[1]              = EJ_NONE;
    }
    else
    {
        assert(cmpTree->OperIsCompare());

        // For details on how we arrived at this mapping, see the comment block in genCodeForTreeNode()
        // while generating code for compare opererators (e.g. GT_EQ etc).
        if ((cmpTree->gtFlags & GTF_RELOP_NAN_UN) != 0)
        {
            // Must branch if we have an NaN, unordered
            switch (cmpTree->gtOper)
            {
                case GT_LT:
                case GT_GT:
                    jmpKind[0] = EJ_jb;
                    jmpKind[1] = EJ_NONE;
                    break;

                case GT_LE:
                case GT_GE:
                    jmpKind[0] = EJ_jbe;
                    jmpKind[1] = EJ_NONE;
                    break;

                case GT_NE:
                    jmpKind[0] = EJ_jpe;
                    jmpKind[1] = EJ_jne;
                    break;

                case GT_EQ:
                    jmpKind[0] = EJ_je;
                    jmpKind[1] = EJ_NONE;
                    break;

                default:
                    unreached();
            }
        }
        else // ((cmpTree->gtFlags & GTF_RELOP_NAN_UN) == 0)
        {
            // Do not branch if we have an NaN, unordered
            switch (cmpTree->gtOper)
            {
                case GT_LT:
                case GT_GT:
                    jmpKind[0] = EJ_ja;
                    jmpKind[1] = EJ_NONE;
                    break;

                case GT_LE:
                case GT_GE:
                    jmpKind[0] = EJ_jae;
                    jmpKind[1] = EJ_NONE;
                    break;

                case GT_NE:
                    jmpKind[0] = EJ_jne;
                    jmpKind[1] = EJ_NONE;
                    break;

                case GT_EQ:
                    jmpKind[0]        = EJ_jpe;
                    jmpKind[1]        = EJ_je;
                    jmpToTrueLabel[0] = false;
                    break;

                default:
                    unreached();
            }
        }
    }
}

#if !defined(_TARGET_64BIT_)
//------------------------------------------------------------------------
// genJumpKindsForTreeLongHi: Generate the jump types for compare
// operators of the high parts of a compare with long type operands
// on x86 for the case where rel-op result needs to be materialized into a
// register.
//
// Arguments:
//    cmpTree - The GT_CMP node
//    jmpKind - Return array of jump kinds
//    jmpToTrueLabel - Return array of if the jump is going to true label
//
// Return Value:
//    None.
//
void CodeGen::genJumpKindsForTreeLongHi(GenTreePtr cmpTree, emitJumpKind jmpKind[2])
{
    assert(cmpTree->OperIsCompare());
    CompareKind compareKind = ((cmpTree->gtFlags & GTF_UNSIGNED) != 0) ? CK_UNSIGNED : CK_SIGNED;

    switch (cmpTree->gtOper)
    {
        case GT_LT:
        case GT_LE:
            if (compareKind == CK_SIGNED)
            {
                jmpKind[0] = EJ_jl;
                jmpKind[1] = EJ_jg;
            }
            else
            {
                jmpKind[0] = EJ_jb;
                jmpKind[1] = EJ_ja;
            }
            break;

        case GT_GT:
        case GT_GE:
            if (compareKind == CK_SIGNED)
            {
                jmpKind[0] = EJ_jg;
                jmpKind[1] = EJ_jl;
            }
            else
            {
                jmpKind[0] = EJ_ja;
                jmpKind[1] = EJ_jb;
            }
            break;

        case GT_EQ:
            // GT_EQ will not jump to the true label if the hi parts are equal
            jmpKind[0] = EJ_NONE;
            jmpKind[1] = EJ_jne;
            break;

        case GT_NE:
            // GT_NE will always jump to the true label if the high parts are not equal
            jmpKind[0] = EJ_jne;
            jmpKind[1] = EJ_NONE;
            break;

        default:
            unreached();
    }
}

//------------------------------------------------------------------------
// genCompareLong: Generate code for comparing two longs on x86 when the result of the compare
// is manifested in a register.
//
// Arguments:
//    treeNode - the compare tree
//
// Return Value:
//    None.
// Comments:
// For long compares, we need to compare the high parts of operands first, then the low parts.
// If the high compare is false, we do not need to compare the low parts. For less than and
// greater than, if the high compare is true, we can assume the entire compare is true. For
// compares that are realized in a register, we will generate:
//
//    Opcode            x86 equivalent          Comment
//    ------            --------------          -------
//    GT_EQ             cmp hiOp1,hiOp2         If any part is not equal, the entire compare
//                      jne label               is false.
//                      cmp loOp1,loOp2
//                      label: sete
//
//    GT_NE             cmp hiOp1,hiOp2         If any part is not equal, the entire compare
//                      jne label               is true.
//                      cmp loOp1,loOp2
//                      label: setne
//
//    GT_LT; unsigned   cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne label               correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      label: setb
//
//    GT_LE; unsigned   cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne label               correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      label: setbe
//
//    GT_GT; unsigned   cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne label               correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      label: seta
//
//    GT_GE; unsigned   cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne label               correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      label: setae
//
// For signed long comparisons, we need additional labels, as we need to use signed conditions on the
// "set" instruction:
//
//    GT_LT; signed     cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne labelHi             correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      setb                    Unsigned set for lo compare
//                      jmp labelFinal
//                      labelHi: setl           Signed set for high compare
//                      labelFinal:
//
//    GT_LE; signed     cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne labelHi             correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      setbe                   Unsigend set for lo compare
//                      jmp labelFinal
//                      labelHi: setle          Signed set for hi compare
//                      labelFinal:
//
//    GT_GT; signed     cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne labelHi             correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      seta                    Unsigned set for lo compare
//                      jmp labelFinal
//                      labelHi: setg           Signed set for high compare
//                      labelFinal
//
//    GT_GE; signed     cmp hiOp1,hiOp2         If hiOp1 is not equal to hiOp2, the flags are set
//                      jne labelHi             correctly and we do not need to check lo. Otherwise,
//                      cmp loOp1,loOp2         we need to compare the lo halves
//                      setae                   Unsigned set for lo compare
//                      jmp labelFinal
//                      labelHi: setge          Signed set for hi compare
//                      labelFinal:
//
// TODO-X86-CQ: Check if hi or lo parts of op2 are 0 and change the compare to a test.
void CodeGen::genCompareLong(GenTreePtr treeNode)
{
    assert(treeNode->OperIsCompare());

    GenTreeOp* tree = treeNode->AsOp();
    GenTreePtr op1  = tree->gtOp1;
    GenTreePtr op2  = tree->gtOp2;

    assert(varTypeIsLong(op1->TypeGet()));
    assert(varTypeIsLong(op2->TypeGet()));

    regNumber targetReg = treeNode->gtRegNum;

    genConsumeOperands(tree);

    GenTreePtr loOp1 = op1->gtGetOp1();
    GenTreePtr hiOp1 = op1->gtGetOp2();
    GenTreePtr loOp2 = op2->gtGetOp1();
    GenTreePtr hiOp2 = op2->gtGetOp2();

    // Create compare for the high parts
    instruction ins     = INS_cmp;
    var_types   cmpType = TYP_INT;
    emitAttr    cmpAttr = emitTypeSize(cmpType);

    // Emit the compare instruction
    getEmitter()->emitInsBinary(ins, cmpAttr, hiOp1, hiOp2);

    // If the result is not being materialized in a register, we're done.
    if (targetReg == REG_NA)
    {
        return;
    }

    // Generate the first jump for the high compare
    CompareKind compareKind = ((tree->gtFlags & GTF_UNSIGNED) != 0) ? CK_UNSIGNED : CK_SIGNED;

    BasicBlock* labelHi    = genCreateTempLabel();
    BasicBlock* labelFinal = genCreateTempLabel();

    if (compareKind == CK_SIGNED && (tree->gtOper != GT_NE && tree->gtOper != GT_EQ))
    {
        // If we are doing a signed comparison, we need to do a signed set if the high compare is true,
        // but an unsigned set if we fall through to the low compare. If we have a GT_NE or GT_EQ, we do not
        // need to worry about the sign of the comparison, so we can use the simplified case.

        // We only have to check for equality for the hi comparison. If they are not equal, then the set will
        // do the right thing. If they are equal, we have to check the lo halves.
        inst_JMP(EJ_jne, labelHi);

        // Emit the comparison. Perform the set for the lo. Jump to labelFinal
        getEmitter()->emitInsBinary(ins, cmpAttr, loOp1, loOp2);

        // The low set must be unsigned
        emitJumpKind jumpKindLo = genJumpKindForOper(tree->gtOper, CK_UNSIGNED);

        inst_SET(jumpKindLo, targetReg);
        inst_JMP(EJ_jmp, labelFinal);

        // Define the label for hi jump target here. If we have jumped here, we want to set
        // the target register based on the jump kind of the actual compare type.

        genDefineTempLabel(labelHi);
        inst_SET(genJumpKindForOper(tree->gtOper, compareKind), targetReg);

        genDefineTempLabel(labelFinal);
        // Set the higher bytes to 0
        inst_RV_RV(ins_Move_Extend(TYP_UBYTE, true), targetReg, targetReg, TYP_UBYTE, emitTypeSize(TYP_UBYTE));
        genProduceReg(tree);
    }
    else
    {
        // If the compare is unsigned, or if the sign doesn't change the set instruction, we can use
        // the same set logic for both the hi and lo compare, so we don't need to jump to a high label,
        // we can just jump to the set that the lo compare will use.

        // We only have to check for equality for the hi comparison. If they are not equal, then the set will
        // do the right thing. If they are equal, we have to check the lo halves.
        inst_JMP(EJ_jne, labelFinal);

        // Emit the comparison
        getEmitter()->emitInsBinary(ins, cmpAttr, loOp1, loOp2);

        // Define the label for hi jump target here. If we have jumped here, we want to set
        // the target register based on the jump kind of the lower half (the actual compare
        // type). If we have fallen through, then we are doing a normal int compare for the
        // lower parts

        genDefineTempLabel(labelFinal);

        // The low set must be unsigned
        emitJumpKind jumpKindLo = genJumpKindForOper(tree->gtOper, CK_UNSIGNED);

        inst_SET(jumpKindLo, targetReg);
        // Set the higher bytes to 0
        inst_RV_RV(ins_Move_Extend(TYP_UBYTE, true), targetReg, targetReg, TYP_UBYTE, emitTypeSize(TYP_UBYTE));
        genProduceReg(tree);
    }
}
#endif //! defined(_TARGET_64BIT_)

//------------------------------------------------------------------------
// genCompareFloat: Generate code for comparing two floating point values
//
// Arguments:
//    treeNode - the compare tree
//
// Return Value:
//    None.
// Comments:
// SSE2 instruction ucomis[s|d] is performs unordered comparison and
// updates rFLAGS register as follows.
//        Result of compare         ZF  PF CF
//        -----------------        ------------
//        Unordered                 1   1   1     <-- this result implies one of operands of compare is a NAN.
//        Greater                   0   0   0
//        Less Than                 0   0   1
//        Equal                     1   0   0
//
// From the above table the following equalities follow. As per ECMA spec *.UN opcodes perform
// unordered comparison of floating point values.  That is *.UN comparisons result in true when
// one of the operands is a NaN whereas ordered comparisons results in false.
//
//    Opcode          Amd64 equivalent         Comment
//    ------          -----------------        --------
//    BLT.UN(a,b)      ucomis[s|d] a, b        Jb branches if CF=1, which means either a<b or unordered from the above
//                     jb                      table
//
//    BLT(a,b)         ucomis[s|d] b, a        Ja branches if CF=0 and ZF=0, which means b>a that in turn implies a<b
//                     ja
//
//    BGT.UN(a,b)      ucomis[s|d] b, a        branch if b<a or unordered ==> branch if a>b or unordered
//                     jb
//
//    BGT(a, b)        ucomis[s|d] a, b        branch if a>b
//                     ja
//
//    BLE.UN(a,b)      ucomis[s|d] a, b        jbe branches if CF=1 or ZF=1, which implies a<=b or unordered
//                     jbe
//
//    BLE(a,b)         ucomis[s|d] b, a        jae branches if CF=0, which mean b>=a or a<=b
//                     jae
//
//    BGE.UN(a,b)      ucomis[s|d] b, a        branch if b<=a or unordered ==> branch if a>=b or unordered
//                     jbe
//
//    BGE(a,b)         ucomis[s|d] a, b        branch if a>=b
//                     jae
//
//    BEQ.UN(a,b)      ucomis[s|d] a, b        branch if a==b or unordered.  There is no BEQ.UN opcode in ECMA spec.
//                     je                      This case is given for completeness, in case if JIT generates such
//                                             a gentree internally.
//
//    BEQ(a,b)         ucomis[s|d] a, b        From the above table, PF=0 and ZF=1 corresponds to a==b.
//                     jpe L1
//                     je <true label>
//                 L1:
//
//    BNE(a,b)         ucomis[s|d] a, b        branch if a!=b.  There is no BNE opcode in ECMA spec. This case is
//                     jne                     given for completeness, in case if JIT generates such a gentree
//                                             internally.
//
//    BNE.UN(a,b)      ucomis[s|d] a, b        From the above table, PF=1 or ZF=0 implies unordered or a!=b
//                     jpe <true label>
//                     jne <true label>
//
// As we can see from the above equalities that the operands of a compare operator need to be
// reveresed in case of BLT/CLT, BGT.UN/CGT.UN, BLE/CLE, BGE.UN/CGE.UN.
void CodeGen::genCompareFloat(GenTreePtr treeNode)
{
    assert(treeNode->OperIsCompare());

    GenTreeOp* tree    = treeNode->AsOp();
    GenTreePtr op1     = tree->gtOp1;
    GenTreePtr op2     = tree->gtOp2;
    var_types  op1Type = op1->TypeGet();
    var_types  op2Type = op2->TypeGet();

    genConsumeOperands(tree);

    assert(varTypeIsFloating(op1Type));
    assert(op1Type == op2Type);

    regNumber   targetReg = treeNode->gtRegNum;
    instruction ins;
    emitAttr    cmpAttr;

    bool reverseOps;
    if ((tree->gtFlags & GTF_RELOP_NAN_UN) != 0)
    {
        // Unordered comparison case
        reverseOps = (tree->gtOper == GT_GT || tree->gtOper == GT_GE);
    }
    else
    {
        reverseOps = (tree->gtOper == GT_LT || tree->gtOper == GT_LE);
    }

    if (reverseOps)
    {
        GenTreePtr tmp = op1;
        op1            = op2;
        op2            = tmp;
    }

    ins     = ins_FloatCompare(op1Type);
    cmpAttr = emitTypeSize(op1Type);

    getEmitter()->emitInsBinary(ins, cmpAttr, op1, op2);

    // Are we evaluating this into a register?
    if (targetReg != REG_NA)
    {
        genSetRegToCond(targetReg, tree);
        genProduceReg(tree);
    }
}

//------------------------------------------------------------------------
// genCompareInt: Generate code for comparing ints or, on amd64, longs.
//
// Arguments:
//    treeNode - the compare tree
//
// Return Value:
//    None.
void CodeGen::genCompareInt(GenTreePtr treeNode)
{
    assert(treeNode->OperIsCompare());

    GenTreeOp* tree      = treeNode->AsOp();
    GenTreePtr op1       = tree->gtOp1;
    GenTreePtr op2       = tree->gtOp2;
    var_types  op1Type   = op1->TypeGet();
    var_types  op2Type   = op2->TypeGet();
    regNumber  targetReg = tree->gtRegNum;

    // Case of op1 == 0 or op1 != 0:
    // Optimize generation of 'test' instruction if op1 sets flags.
    //
    // Note that if LSRA has inserted any GT_RELOAD/GT_COPY before
    // op1, it will not modify the flags set by codegen of op1.
    // Similarly op1 could also be reg-optional at its use and
    // it was spilled after producing its result in a register.
    // Spill code too will not modify the flags set by op1.
    GenTree* realOp1 = op1->gtSkipReloadOrCopy();
    if (realOp1->gtSetFlags())
    {
        // op1 must set ZF and SF flags
        assert(realOp1->gtSetZSFlags());

        // Must be (in)equality against zero.
        assert(tree->OperIs(GT_EQ, GT_NE));
        assert(op2->IsIntegralConst(0));
        assert(op2->isContained());

        // Just consume the operands
        genConsumeOperands(tree);

        // No need to generate test instruction since
        // op1 sets flags

        // Are we evaluating this into a register?
        if (targetReg != REG_NA)
        {
            genSetRegToCond(targetReg, tree);
            genProduceReg(tree);
        }

        return;
    }

#ifdef FEATURE_SIMD
    // If we have GT_JTRUE(GT_EQ/NE(GT_SIMD((in)Equality, v1, v2), true/false)),
    // then we don't need to generate code for GT_EQ/GT_NE, since SIMD (in)Equality intrinsic
    // would set or clear Zero flag.
    if ((targetReg == REG_NA) && tree->OperIs(GT_EQ, GT_NE))
    {
        // Is it a SIMD (in)Equality that doesn't need to materialize result into a register?
        if ((op1->gtRegNum == REG_NA) && op1->IsSIMDEqualityOrInequality())
        {
            // Must be comparing against true or false.
            assert(op2->IsIntegralConst(0) || op2->IsIntegralConst(1));
            assert(op2->isContainedIntOrIImmed());

            // In this case SIMD (in)Equality will set or clear
            // Zero flag, based on which GT_JTRUE would generate
            // the right conditional jump.
            return;
        }
    }
#endif // FEATURE_SIMD

    genConsumeOperands(tree);

    // TODO-CQ: We should be able to support swapping op1 and op2 to generate cmp reg, imm.
    // https://github.com/dotnet/coreclr/issues/7270
    assert(!op1->isContainedIntOrIImmed()); // We no longer support
    assert(!varTypeIsFloating(op2Type));

    instruction ins;

    if (tree->OperIs(GT_TEST_EQ, GT_TEST_NE))
    {
        ins = INS_test;
    }
    else if (op1->isUsedFromReg() && op2->IsIntegralConst(0))
    {
        // We're comparing a register to 0 so we can generate "test reg1, reg1"
        // instead of the longer "cmp reg1, 0"
        ins = INS_test;
        op2 = op1;
    }
    else
    {
        ins = INS_cmp;
    }

    var_types type;

    if (op1Type == op2Type)
    {
        type = op1Type;
    }
    else if (genTypeSize(op1Type) == genTypeSize(op2Type))
    {
        // If the types are different but have the same size then we'll use TYP_INT or TYP_LONG.
        // This primarily deals with small type mixes (e.g. byte/ubyte) that need to be widened
        // and compared as int. We should not get long type mixes here but handle that as well
        // just in case.
        type = genTypeSize(op1Type) == 8 ? TYP_LONG : TYP_INT;
    }
    else
    {
        // In the types are different simply use TYP_INT. This deals with small type/int type
        // mixes (e.g. byte/short ubyte/int) that need to be widened and compared as int.
        // Lowering is expected to handle any mixes that involve long types (e.g. int/long).
        type = TYP_INT;
    }

    // The common type cannot be larger than the machine word size
    assert(genTypeSize(type) <= genTypeSize(TYP_I_IMPL));
    // The common type cannot be smaller than any of the operand types, we're probably mixing int/long
    assert(genTypeSize(type) >= max(genTypeSize(op1Type), genTypeSize(op2Type)));
    // TYP_UINT and TYP_ULONG should not appear here, only small types can be unsigned
    assert(!varTypeIsUnsigned(type) || varTypeIsSmall(type));
    // Small unsigned int types (TYP_BOOL can use anything) should use unsigned comparisons
    assert(!(varTypeIsSmallInt(type) && varTypeIsUnsigned(type)) || ((tree->gtFlags & GTF_UNSIGNED) != 0));
    // If op1 is smaller then it cannot be in memory, we're probably missing a cast
    assert((genTypeSize(op1Type) >= genTypeSize(type)) || !op1->isUsedFromMemory());
    // If op2 is smaller then it cannot be in memory, we're probably missing a cast
    assert((genTypeSize(op2Type) >= genTypeSize(type)) || !op2->isUsedFromMemory());
    // If op2 is a constant then it should fit in the common type
    assert(!op2->IsCnsIntOrI() || genTypeCanRepresentValue(type, op2->AsIntCon()->IconValue()));

    getEmitter()->emitInsBinary(ins, emitTypeSize(type), op1, op2);

    // Are we evaluating this into a register?
    if (targetReg != REG_NA)
    {
        genSetRegToCond(targetReg, tree);
        genProduceReg(tree);
    }
}

//-------------------------------------------------------------------------------------------
// genSetRegToCond:  Set a register 'dstReg' to the appropriate one or zero value
//                   corresponding to a binary Relational operator result.
//
// Arguments:
//   dstReg          - The target register to set to 1 or 0
//   tree            - The GenTree Relop node that was used to set the Condition codes
//
// Return Value:     none
//
// Notes:
//    A full 64-bit value of either 1 or 0 is setup in the 'dstReg'
//-------------------------------------------------------------------------------------------

void CodeGen::genSetRegToCond(regNumber dstReg, GenTreePtr tree)
{
    noway_assert((genRegMask(dstReg) & RBM_BYTE_REGS) != 0);

    emitJumpKind jumpKind[2];
    bool         branchToTrueLabel[2];
    genJumpKindsForTree(tree, jumpKind, branchToTrueLabel);

    if (jumpKind[1] == EJ_NONE)
    {
        // Set (lower byte of) reg according to the flags
        inst_SET(jumpKind[0], dstReg);
    }
    else
    {
#ifdef DEBUG
        // jmpKind[1] != EJ_NONE implies BEQ and BEN.UN of floating point values.
        // These are represented by two conditions.
        if (tree->gtOper == GT_EQ)
        {
            // This must be an ordered comparison.
            assert((tree->gtFlags & GTF_RELOP_NAN_UN) == 0);
        }
        else
        {
            // This must be BNE.UN
            assert((tree->gtOper == GT_NE) && ((tree->gtFlags & GTF_RELOP_NAN_UN) != 0));
        }
#endif

        // Here is the sample code generated in each case:
        // BEQ ==  cmp, jpe <false label>, je <true label>
        // That is, to materialize comparison reg needs to be set if PF=0 and ZF=1
        //      setnp reg  // if (PF==0) reg = 1 else reg = 0
        //      jpe L1     // Jmp if PF==1
        //      sete reg
        //  L1:
        //
        // BNE.UN == cmp, jpe <true label>, jne <true label>
        // That is, to materialize the comparison reg needs to be set if either PF=1 or ZF=0;
        //     setp reg
        //     jpe L1
        //     setne reg
        //  L1:

        // reverse the jmpkind condition before setting dstReg if it is to false label.
        inst_SET(branchToTrueLabel[0] ? jumpKind[0] : emitter::emitReverseJumpKind(jumpKind[0]), dstReg);

        BasicBlock* label = genCreateTempLabel();
        inst_JMP(jumpKind[0], label);

        // second branch is always to true label
        assert(branchToTrueLabel[1]);
        inst_SET(jumpKind[1], dstReg);
        genDefineTempLabel(label);
    }

    var_types treeType = tree->TypeGet();
    if (treeType == TYP_INT || treeType == TYP_LONG)
    {
        // Set the higher bytes to 0
        inst_RV_RV(ins_Move_Extend(TYP_UBYTE, true), dstReg, dstReg, TYP_UBYTE, emitTypeSize(TYP_UBYTE));
    }
    else
    {
        noway_assert(treeType == TYP_BYTE);
    }
}

#if !defined(_TARGET_64BIT_)
//------------------------------------------------------------------------
// genLongToIntCast: Generate code for long to int casts on x86.
//
// Arguments:
//    cast - The GT_CAST node
//
// Return Value:
//    None.
//
// Assumptions:
//    The cast node and its sources (via GT_LONG) must have been assigned registers.
//    The destination cannot be a floating point type or a small integer type.
//
void CodeGen::genLongToIntCast(GenTree* cast)
{
    assert(cast->OperGet() == GT_CAST);

    GenTree* src = cast->gtGetOp1();
    noway_assert(src->OperGet() == GT_LONG);

    genConsumeRegs(src);

    var_types srcType  = ((cast->gtFlags & GTF_UNSIGNED) != 0) ? TYP_ULONG : TYP_LONG;
    var_types dstType  = cast->CastToType();
    regNumber loSrcReg = src->gtGetOp1()->gtRegNum;
    regNumber hiSrcReg = src->gtGetOp2()->gtRegNum;
    regNumber dstReg   = cast->gtRegNum;

    assert((dstType == TYP_INT) || (dstType == TYP_UINT));
    assert(genIsValidIntReg(loSrcReg));
    assert(genIsValidIntReg(hiSrcReg));
    assert(genIsValidIntReg(dstReg));

    if (cast->gtOverflow())
    {
        //
        // Generate an overflow check for [u]long to [u]int casts:
        //
        // long  -> int  - check if the upper 33 bits are all 0 or all 1
        //
        // ulong -> int  - check if the upper 33 bits are all 0
        //
        // long  -> uint - check if the upper 32 bits are all 0
        // ulong -> uint - check if the upper 32 bits are all 0
        //

        if ((srcType == TYP_LONG) && (dstType == TYP_INT))
        {
            BasicBlock* allOne  = genCreateTempLabel();
            BasicBlock* success = genCreateTempLabel();

            inst_RV_RV(INS_test, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
            inst_JMP(EJ_js, allOne);

            inst_RV_RV(INS_test, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
            genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
            inst_JMP(EJ_jmp, success);

            genDefineTempLabel(allOne);
            inst_RV_IV(INS_cmp, hiSrcReg, -1, EA_4BYTE);
            genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);

            genDefineTempLabel(success);
        }
        else
        {
            if ((srcType == TYP_ULONG) && (dstType == TYP_INT))
            {
                inst_RV_RV(INS_test, loSrcReg, loSrcReg, TYP_INT, EA_4BYTE);
                genJumpToThrowHlpBlk(EJ_js, SCK_OVERFLOW);
            }

            inst_RV_RV(INS_test, hiSrcReg, hiSrcReg, TYP_INT, EA_4BYTE);
            genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
        }
    }

    if (dstReg != loSrcReg)
    {
        inst_RV_RV(INS_mov, dstReg, loSrcReg, TYP_INT, EA_4BYTE);
    }

    genProduceReg(cast);
}
#endif

//------------------------------------------------------------------------
// genIntToIntCast: Generate code for an integer cast
//    This method handles integer overflow checking casts
//    as well as ordinary integer casts.
//
// Arguments:
//    treeNode - The GT_CAST node
//
// Return Value:
//    None.
//
// Assumptions:
//    The treeNode is not a contained node and must have an assigned register.
//    For a signed convert from byte, the source must be in a byte-addressable register.
//    Neither the source nor target type can be a floating point type.
//
// TODO-XArch-CQ: Allow castOp to be a contained node without an assigned register.
// TODO: refactor to use getCastDescription
//
void CodeGen::genIntToIntCast(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_CAST);

    GenTreePtr castOp  = treeNode->gtCast.CastOp();
    var_types  srcType = genActualType(castOp->TypeGet());
    noway_assert(genTypeSize(srcType) >= 4);

#ifdef _TARGET_X86_
    if (varTypeIsLong(srcType))
    {
        genLongToIntCast(treeNode);
        return;
    }
#endif // _TARGET_X86_

    regNumber targetReg     = treeNode->gtRegNum;
    regNumber sourceReg     = castOp->gtRegNum;
    var_types dstType       = treeNode->CastToType();
    bool      isUnsignedDst = varTypeIsUnsigned(dstType);
    bool      isUnsignedSrc = varTypeIsUnsigned(srcType);

    // if necessary, force the srcType to unsigned when the GT_UNSIGNED flag is set
    if (!isUnsignedSrc && (treeNode->gtFlags & GTF_UNSIGNED) != 0)
    {
        srcType       = genUnsignedType(srcType);
        isUnsignedSrc = true;
    }

    bool requiresOverflowCheck = false;

    assert(genIsValidIntReg(targetReg));
    assert(genIsValidIntReg(sourceReg));

    instruction ins     = INS_invalid;
    emitAttr    srcSize = EA_ATTR(genTypeSize(srcType));
    emitAttr    dstSize = EA_ATTR(genTypeSize(dstType));

    if (srcSize < dstSize)
    {
        // Widening cast
        // Is this an Overflow checking cast?
        // We only need to handle one case, as the other casts can never overflow.
        //   cast from TYP_INT to TYP_ULONG
        //
        if (treeNode->gtOverflow() && (srcType == TYP_INT) && (dstType == TYP_ULONG))
        {
            requiresOverflowCheck = true;
            ins                   = INS_mov;
        }
        else
        {
            noway_assert(srcSize < EA_PTRSIZE);

            ins = ins_Move_Extend(srcType, castOp->InReg());

            /*
                Special case: ins_Move_Extend assumes the destination type is no bigger
                than TYP_INT.  movsx and movzx can already extend all the way to
                64-bit, and a regular 32-bit mov clears the high 32 bits (like the non-existant movzxd),
                but for a sign extension from TYP_INT to TYP_LONG, we need to use movsxd opcode.
            */
            if (!isUnsignedSrc && !isUnsignedDst)
            {
#ifdef _TARGET_X86_
                NYI_X86("Cast to 64 bit for x86/RyuJIT");
#else  // !_TARGET_X86_
                ins = INS_movsxd;
#endif // !_TARGET_X86_
            }
        }
    }
    else
    {
        // Narrowing cast, or sign-changing cast
        noway_assert(srcSize >= dstSize);

        // Is this an Overflow checking cast?
        if (treeNode->gtOverflow())
        {
            requiresOverflowCheck = true;
            ins                   = INS_mov;
        }
        else
        {
            ins = ins_Move_Extend(dstType, castOp->InReg());
        }
    }

    noway_assert(ins != INS_invalid);

    genConsumeReg(castOp);

    if (requiresOverflowCheck)
    {
        ssize_t typeMin        = 0;
        ssize_t typeMax        = 0;
        ssize_t typeMask       = 0;
        bool    needScratchReg = false;
        bool    signCheckOnly  = false;

        /* Do we need to compare the value, or just check masks */

        switch (dstType)
        {
            case TYP_BYTE:
                typeMask = ssize_t((int)0xFFFFFF80);
                typeMin  = SCHAR_MIN;
                typeMax  = SCHAR_MAX;
                break;

            case TYP_UBYTE:
                typeMask = ssize_t((int)0xFFFFFF00L);
                break;

            case TYP_SHORT:
                typeMask = ssize_t((int)0xFFFF8000);
                typeMin  = SHRT_MIN;
                typeMax  = SHRT_MAX;
                break;

            case TYP_CHAR:
                typeMask = ssize_t((int)0xFFFF0000L);
                break;

            case TYP_INT:
                if (srcType == TYP_UINT)
                {
                    signCheckOnly = true;
                }
                else
                {
                    typeMask = 0xFFFFFFFF80000000LL;
                    typeMin  = INT_MIN;
                    typeMax  = INT_MAX;
                }
                break;

            case TYP_UINT:
                if (srcType == TYP_INT)
                {
                    signCheckOnly = true;
                }
                else
                {
                    needScratchReg = true;
                }
                break;

            case TYP_LONG:
                noway_assert(srcType == TYP_ULONG);
                signCheckOnly = true;
                break;

            case TYP_ULONG:
                noway_assert((srcType == TYP_LONG) || (srcType == TYP_INT));
                signCheckOnly = true;
                break;

            default:
                NO_WAY("Unknown type");
                return;
        }

        if (signCheckOnly)
        {
            // We only need to check for a negative value in sourceReg
            inst_RV_IV(INS_cmp, sourceReg, 0, srcSize);
            genJumpToThrowHlpBlk(EJ_jl, SCK_OVERFLOW);
        }
        else
        {
            // When we are converting from unsigned or to unsigned, we
            // will only have to check for any bits set using 'typeMask'
            if (isUnsignedSrc || isUnsignedDst)
            {
                if (needScratchReg)
                {
                    regNumber tmpReg = treeNode->GetSingleTempReg();
                    inst_RV_RV(INS_mov, tmpReg, sourceReg, TYP_LONG); // Move the 64-bit value to a writeable temp reg
                    inst_RV_SH(INS_SHIFT_RIGHT_LOGICAL, srcSize, tmpReg, 32); // Shift right by 32 bits
                    genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);               // Throw if result shift is non-zero
                }
                else
                {
                    noway_assert(typeMask != 0);
                    inst_RV_IV(INS_TEST, sourceReg, typeMask, srcSize);
                    genJumpToThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
                }
            }
            else
            {
                // For a narrowing signed cast
                //
                // We must check the value is in a signed range.

                // Compare with the MAX

                noway_assert((typeMin != 0) && (typeMax != 0));

                inst_RV_IV(INS_cmp, sourceReg, typeMax, srcSize);
                genJumpToThrowHlpBlk(EJ_jg, SCK_OVERFLOW);

                // Compare with the MIN

                inst_RV_IV(INS_cmp, sourceReg, typeMin, srcSize);
                genJumpToThrowHlpBlk(EJ_jl, SCK_OVERFLOW);
            }
        }

        if (targetReg != sourceReg
#ifdef _TARGET_AMD64_
            // On amd64, we can hit this path for a same-register
            // 4-byte to 8-byte widening conversion, and need to
            // emit the instruction to set the high bits correctly.
            || (dstSize == EA_8BYTE && srcSize == EA_4BYTE)
#endif // _TARGET_AMD64_
                )
            inst_RV_RV(ins, targetReg, sourceReg, srcType, srcSize);
    }
    else // non-overflow checking cast
    {
        // We may have code transformations that result in casts where srcType is the same as dstType.
        // e.g. Bug 824281, in which a comma is split by the rationalizer, leaving an assignment of a
        // long constant to a long lclVar.
        if (srcType == dstType)
        {
            ins = INS_mov;
        }
        /* Is the value sitting in a non-byte-addressable register? */
        else if (castOp->InReg() && (dstSize == EA_1BYTE) && !isByteReg(sourceReg))
        {
            if (isUnsignedDst)
            {
                // for unsigned values we can AND, so it need not be a byte register
                ins = INS_AND;
            }
            else
            {
                // Move the value into a byte register
                noway_assert(!"Signed byte convert from non-byte-addressable register");
            }

            /* Generate "mov targetReg, castOp->gtReg */
            if (targetReg != sourceReg)
            {
                inst_RV_RV(INS_mov, targetReg, sourceReg, srcType, srcSize);
            }
        }

        if (ins == INS_AND)
        {
            noway_assert(isUnsignedDst);

            /* Generate "and reg, MASK */
            unsigned fillPattern;
            if (dstSize == EA_1BYTE)
            {
                fillPattern = 0xff;
            }
            else if (dstSize == EA_2BYTE)
            {
                fillPattern = 0xffff;
            }
            else
            {
                fillPattern = 0xffffffff;
            }

            inst_RV_IV(INS_AND, targetReg, fillPattern, EA_4BYTE);
        }
#ifdef _TARGET_AMD64_
        else if (ins == INS_movsxd)
        {
            inst_RV_RV(ins, targetReg, sourceReg, srcType, srcSize);
        }
#endif // _TARGET_AMD64_
        else if (ins == INS_mov)
        {
            if (targetReg != sourceReg
#ifdef _TARGET_AMD64_
                // On amd64, 'mov' is the opcode used to zero-extend from
                // 4 bytes to 8 bytes.
                || (dstSize == EA_8BYTE && srcSize == EA_4BYTE)
#endif // _TARGET_AMD64_
                    )
            {
                inst_RV_RV(ins, targetReg, sourceReg, srcType, srcSize);
            }
        }
        else
        {
            noway_assert(ins == INS_movsx || ins == INS_movzx);
            noway_assert(srcSize >= dstSize);

            /* Generate "mov targetReg, castOp->gtReg */
            inst_RV_RV(ins, targetReg, sourceReg, srcType, dstSize);
        }
    }

    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genFloatToFloatCast: Generate code for a cast between float and double
//
// Arguments:
//    treeNode - The GT_CAST node
//
// Return Value:
//    None.
//
// Assumptions:
//    Cast is a non-overflow conversion.
//    The treeNode must have an assigned register.
//    The cast is between float and double or vice versa.
//
void CodeGen::genFloatToFloatCast(GenTreePtr treeNode)
{
    // float <--> double conversions are always non-overflow ones
    assert(treeNode->OperGet() == GT_CAST);
    assert(!treeNode->gtOverflow());

    regNumber targetReg = treeNode->gtRegNum;
    assert(genIsValidFloatReg(targetReg));

    GenTreePtr op1 = treeNode->gtOp.gtOp1;
#ifdef DEBUG
    // If not contained, must be a valid float reg.
    if (op1->isUsedFromReg())
    {
        assert(genIsValidFloatReg(op1->gtRegNum));
    }
#endif

    var_types dstType = treeNode->CastToType();
    var_types srcType = op1->TypeGet();
    assert(varTypeIsFloating(srcType) && varTypeIsFloating(dstType));

    genConsumeOperands(treeNode->AsOp());
    if (srcType == dstType && (op1->isUsedFromReg() && (targetReg == op1->gtRegNum)))
    {
        // source and destinations types are the same and also reside in the same register.
        // we just need to consume and produce the reg in this case.
        ;
    }
    else
    {
        instruction ins = ins_FloatConv(dstType, srcType);
        getEmitter()->emitInsBinary(ins, emitTypeSize(dstType), treeNode, op1);
    }

    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genIntToFloatCast: Generate code to cast an int/long to float/double
//
// Arguments:
//    treeNode - The GT_CAST node
//
// Return Value:
//    None.
//
// Assumptions:
//    Cast is a non-overflow conversion.
//    The treeNode must have an assigned register.
//    SrcType= int32/uint32/int64/uint64 and DstType=float/double.
//
void CodeGen::genIntToFloatCast(GenTreePtr treeNode)
{
    // int type --> float/double conversions are always non-overflow ones
    assert(treeNode->OperGet() == GT_CAST);
    assert(!treeNode->gtOverflow());

    regNumber targetReg = treeNode->gtRegNum;
    assert(genIsValidFloatReg(targetReg));

    GenTreePtr op1 = treeNode->gtOp.gtOp1;
#ifdef DEBUG
    if (op1->isUsedFromReg())
    {
        assert(genIsValidIntReg(op1->gtRegNum));
    }
#endif

    var_types dstType = treeNode->CastToType();
    var_types srcType = op1->TypeGet();
    assert(!varTypeIsFloating(srcType) && varTypeIsFloating(dstType));

#if !defined(_TARGET_64BIT_)
    // We expect morph to replace long to float/double casts with helper calls
    noway_assert(!varTypeIsLong(srcType));
#endif // !defined(_TARGET_64BIT_)

    // Since xarch emitter doesn't handle reporting gc-info correctly while casting away gc-ness we
    // ensure srcType of a cast is non gc-type.  Codegen should never see BYREF as source type except
    // for GT_LCL_VAR_ADDR and GT_LCL_FLD_ADDR that represent stack addresses and can be considered
    // as TYP_I_IMPL. In all other cases where src operand is a gc-type and not known to be on stack,
    // Front-end (see fgMorphCast()) ensures this by assigning gc-type local to a non gc-type
    // temp and using temp as operand of cast operation.
    if (srcType == TYP_BYREF)
    {
        noway_assert(op1->OperGet() == GT_LCL_VAR_ADDR || op1->OperGet() == GT_LCL_FLD_ADDR);
        srcType = TYP_I_IMPL;
    }

    // force the srcType to unsigned if GT_UNSIGNED flag is set
    if (treeNode->gtFlags & GTF_UNSIGNED)
    {
        srcType = genUnsignedType(srcType);
    }

    noway_assert(!varTypeIsGC(srcType));

    // We should never be seeing srcType whose size is not sizeof(int) nor sizeof(long).
    // For conversions from byte/sbyte/int16/uint16 to float/double, we would expect
    // either the front-end or lowering phase to have generated two levels of cast.
    // The first one is for widening smaller int type to int32 and the second one is
    // to the float/double.
    emitAttr srcSize = EA_ATTR(genTypeSize(srcType));
    noway_assert((srcSize == EA_ATTR(genTypeSize(TYP_INT))) || (srcSize == EA_ATTR(genTypeSize(TYP_LONG))));

    // Also we don't expect to see uint32 -> float/double and uint64 -> float conversions
    // here since they should have been lowered apropriately.
    noway_assert(srcType != TYP_UINT);
    noway_assert((srcType != TYP_ULONG) || (dstType != TYP_FLOAT));

    // To convert int to a float/double, cvtsi2ss/sd SSE2 instruction is used
    // which does a partial write to lower 4/8 bytes of xmm register keeping the other
    // upper bytes unmodified.  If "cvtsi2ss/sd xmmReg, r32/r64" occurs inside a loop,
    // the partial write could introduce a false dependency and could cause a stall
    // if there are further uses of xmmReg. We have such a case occuring with a
    // customer reported version of SpectralNorm benchmark, resulting in 2x perf
    // regression.  To avoid false dependency, we emit "xorps xmmReg, xmmReg" before
    // cvtsi2ss/sd instruction.

    genConsumeOperands(treeNode->AsOp());
    getEmitter()->emitIns_R_R(INS_xorps, EA_4BYTE, treeNode->gtRegNum, treeNode->gtRegNum);

    // Note that here we need to specify srcType that will determine
    // the size of source reg/mem operand and rex.w prefix.
    instruction ins = ins_FloatConv(dstType, TYP_INT);
    getEmitter()->emitInsBinary(ins, emitTypeSize(srcType), treeNode, op1);

    // Handle the case of srcType = TYP_ULONG. SSE2 conversion instruction
    // will interpret ULONG value as LONG.  Hence we need to adjust the
    // result if sign-bit of srcType is set.
    if (srcType == TYP_ULONG)
    {
        // The instruction sequence below is less accurate than what clang
        // and gcc generate. However, we keep the current sequence for backward compatiblity.
        // If we change the instructions below, FloatingPointUtils::convertUInt64ToDobule
        // should be also updated for consistent conversion result.
        assert(dstType == TYP_DOUBLE);
        assert(op1->isUsedFromReg());

        // Set the flags without modifying op1.
        // test op1Reg, op1Reg
        inst_RV_RV(INS_test, op1->gtRegNum, op1->gtRegNum, srcType);

        // No need to adjust result if op1 >= 0 i.e. positive
        // Jge label
        BasicBlock* label = genCreateTempLabel();
        inst_JMP(EJ_jge, label);

        // Adjust the result
        // result = result + 0x43f00000 00000000
        // addsd resultReg,  0x43f00000 00000000
        GenTreePtr* cns = &u8ToDblBitmask;
        if (*cns == nullptr)
        {
            double d;
            static_assert_no_msg(sizeof(double) == sizeof(__int64));
            *((__int64*)&d) = 0x43f0000000000000LL;

            *cns = genMakeConst(&d, dstType, treeNode, true);
        }
        inst_RV_TT(INS_addsd, treeNode->gtRegNum, *cns);

        genDefineTempLabel(label);
    }

    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genFloatToIntCast: Generate code to cast float/double to int/long
//
// Arguments:
//    treeNode - The GT_CAST node
//
// Return Value:
//    None.
//
// Assumptions:
//    Cast is a non-overflow conversion.
//    The treeNode must have an assigned register.
//    SrcType=float/double and DstType= int32/uint32/int64/uint64
//
// TODO-XArch-CQ: (Low-pri) - generate in-line code when DstType = uint64
//
void CodeGen::genFloatToIntCast(GenTreePtr treeNode)
{
    // we don't expect to see overflow detecting float/double --> int type conversions here
    // as they should have been converted into helper calls by front-end.
    assert(treeNode->OperGet() == GT_CAST);
    assert(!treeNode->gtOverflow());

    regNumber targetReg = treeNode->gtRegNum;
    assert(genIsValidIntReg(targetReg));

    GenTreePtr op1 = treeNode->gtOp.gtOp1;
#ifdef DEBUG
    if (op1->isUsedFromReg())
    {
        assert(genIsValidFloatReg(op1->gtRegNum));
    }
#endif

    var_types dstType = treeNode->CastToType();
    var_types srcType = op1->TypeGet();
    assert(varTypeIsFloating(srcType) && !varTypeIsFloating(dstType));

    // We should never be seeing dstType whose size is neither sizeof(TYP_INT) nor sizeof(TYP_LONG).
    // For conversions to byte/sbyte/int16/uint16 from float/double, we would expect the
    // front-end or lowering phase to have generated two levels of cast. The first one is
    // for float or double to int32/uint32 and the second one for narrowing int32/uint32 to
    // the required smaller int type.
    emitAttr dstSize = EA_ATTR(genTypeSize(dstType));
    noway_assert((dstSize == EA_ATTR(genTypeSize(TYP_INT))) || (dstSize == EA_ATTR(genTypeSize(TYP_LONG))));

    // We shouldn't be seeing uint64 here as it should have been converted
    // into a helper call by either front-end or lowering phase.
    noway_assert(!varTypeIsUnsigned(dstType) || (dstSize != EA_ATTR(genTypeSize(TYP_LONG))));

    // If the dstType is TYP_UINT, we have 32-bits to encode the
    // float number. Any of 33rd or above bits can be the sign bit.
    // To acheive it we pretend as if we are converting it to a long.
    if (varTypeIsUnsigned(dstType) && (dstSize == EA_ATTR(genTypeSize(TYP_INT))))
    {
        dstType = TYP_LONG;
    }

    // Note that we need to specify dstType here so that it will determine
    // the size of destination integer register and also the rex.w prefix.
    genConsumeOperands(treeNode->AsOp());
    instruction ins = ins_FloatConv(TYP_INT, srcType);
    getEmitter()->emitInsBinary(ins, emitTypeSize(dstType), treeNode, op1);
    genProduceReg(treeNode);
}

//------------------------------------------------------------------------
// genCkfinite: Generate code for ckfinite opcode.
//
// Arguments:
//    treeNode - The GT_CKFINITE node
//
// Return Value:
//    None.
//
// Assumptions:
//    GT_CKFINITE node has reserved an internal register.
//
// TODO-XArch-CQ - mark the operand as contained if known to be in
// memory (e.g. field or an array element).
//
void CodeGen::genCkfinite(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_CKFINITE);

    GenTreePtr op1        = treeNode->gtOp.gtOp1;
    var_types  targetType = treeNode->TypeGet();
    int        expMask    = (targetType == TYP_FLOAT) ? 0x7F800000 : 0x7FF00000; // Bit mask to extract exponent.
    regNumber  targetReg  = treeNode->gtRegNum;

    // Extract exponent into a register.
    regNumber tmpReg = treeNode->GetSingleTempReg();

    genConsumeReg(op1);

#ifdef _TARGET_64BIT_

    // Copy the floating-point value to an integer register. If we copied a float to a long, then
    // right-shift the value so the high 32 bits of the floating-point value sit in the low 32
    // bits of the integer register.
    instruction ins = ins_CopyFloatToInt(targetType, (targetType == TYP_FLOAT) ? TYP_INT : TYP_LONG);
    inst_RV_RV(ins, op1->gtRegNum, tmpReg, targetType);
    if (targetType == TYP_DOUBLE)
    {
        // right shift by 32 bits to get to exponent.
        inst_RV_SH(INS_shr, EA_8BYTE, tmpReg, 32);
    }

    // Mask exponent with all 1's and check if the exponent is all 1's
    inst_RV_IV(INS_and, tmpReg, expMask, EA_4BYTE);
    inst_RV_IV(INS_cmp, tmpReg, expMask, EA_4BYTE);

    // If exponent is all 1's, throw ArithmeticException
    genJumpToThrowHlpBlk(EJ_je, SCK_ARITH_EXCPN);

    // if it is a finite value copy it to targetReg
    if (targetReg != op1->gtRegNum)
    {
        inst_RV_RV(ins_Copy(targetType), targetReg, op1->gtRegNum, targetType);
    }

#else // !_TARGET_64BIT_

    // If the target type is TYP_DOUBLE, we want to extract the high 32 bits into the register.
    // There is no easy way to do this. To not require an extra register, we'll use shuffles
    // to move the high 32 bits into the low 32 bits, then then shuffle it back, since we
    // need to produce the value into the target register.
    //
    // For TYP_DOUBLE, we'll generate (for targetReg != op1->gtRegNum):
    //    movaps targetReg, op1->gtRegNum
    //    shufps targetReg, targetReg, 0xB1    // WZYX => ZWXY
    //    mov_xmm2i tmpReg, targetReg          // tmpReg <= Y
    //    and tmpReg, <mask>
    //    cmp tmpReg, <mask>
    //    je <throw block>
    //    movaps targetReg, op1->gtRegNum   // copy the value again, instead of un-shuffling it
    //
    // For TYP_DOUBLE with (targetReg == op1->gtRegNum):
    //    shufps targetReg, targetReg, 0xB1    // WZYX => ZWXY
    //    mov_xmm2i tmpReg, targetReg          // tmpReg <= Y
    //    and tmpReg, <mask>
    //    cmp tmpReg, <mask>
    //    je <throw block>
    //    shufps targetReg, targetReg, 0xB1    // ZWXY => WZYX
    //
    // For TYP_FLOAT, it's the same as _TARGET_64BIT_:
    //    mov_xmm2i tmpReg, targetReg          // tmpReg <= low 32 bits
    //    and tmpReg, <mask>
    //    cmp tmpReg, <mask>
    //    je <throw block>
    //    movaps targetReg, op1->gtRegNum      // only if targetReg != op1->gtRegNum

    regNumber copyToTmpSrcReg; // The register we'll copy to the integer temp.

    if (targetType == TYP_DOUBLE)
    {
        if (targetReg != op1->gtRegNum)
        {
            inst_RV_RV(ins_Copy(targetType), targetReg, op1->gtRegNum, targetType);
        }
        inst_RV_RV_IV(INS_shufps, EA_16BYTE, targetReg, targetReg, 0xb1);
        copyToTmpSrcReg = targetReg;
    }
    else
    {
        copyToTmpSrcReg = op1->gtRegNum;
    }

    // Copy only the low 32 bits. This will be the high order 32 bits of the floating-point
    // value, no matter the floating-point type.
    inst_RV_RV(ins_CopyFloatToInt(TYP_FLOAT, TYP_INT), copyToTmpSrcReg, tmpReg, TYP_FLOAT);

    // Mask exponent with all 1's and check if the exponent is all 1's
    inst_RV_IV(INS_and, tmpReg, expMask, EA_4BYTE);
    inst_RV_IV(INS_cmp, tmpReg, expMask, EA_4BYTE);

    // If exponent is all 1's, throw ArithmeticException
    genJumpToThrowHlpBlk(EJ_je, SCK_ARITH_EXCPN);

    if (targetReg != op1->gtRegNum)
    {
        // In both the TYP_FLOAT and TYP_DOUBLE case, the op1 register is untouched,
        // so copy it to the targetReg. This is faster and smaller for TYP_DOUBLE
        // than re-shuffling the targetReg.
        inst_RV_RV(ins_Copy(targetType), targetReg, op1->gtRegNum, targetType);
    }
    else if (targetType == TYP_DOUBLE)
    {
        // We need to re-shuffle the targetReg to get the correct result.
        inst_RV_RV_IV(INS_shufps, EA_16BYTE, targetReg, targetReg, 0xb1);
    }

#endif // !_TARGET_64BIT_

    genProduceReg(treeNode);
}

#ifdef _TARGET_AMD64_
int CodeGenInterface::genSPtoFPdelta()
{
    int delta;

#ifdef UNIX_AMD64_ABI

    // We require frame chaining on Unix to support native tool unwinding (such as
    // unwinding by the native debugger). We have a CLR-only extension to the
    // unwind codes (UWOP_SET_FPREG_LARGE) to support SP->FP offsets larger than 240.
    // If Unix ever supports EnC, the RSP == RBP assumption will have to be reevaluated.
    delta = genTotalFrameSize();

#else // !UNIX_AMD64_ABI

    // As per Amd64 ABI, RBP offset from initial RSP can be between 0 and 240 if
    // RBP needs to be reported in unwind codes.  This case would arise for methods
    // with localloc.
    if (compiler->compLocallocUsed)
    {
        // We cannot base delta computation on compLclFrameSize since it changes from
        // tentative to final frame layout and hence there is a possibility of
        // under-estimating offset of vars from FP, which in turn results in under-
        // estimating instruction size.
        //
        // To be predictive and so as never to under-estimate offset of vars from FP
        // we will always position FP at min(240, outgoing arg area size).
        delta = Min(240, (int)compiler->lvaOutgoingArgSpaceSize);
    }
    else if (compiler->opts.compDbgEnC)
    {
        // vm assumption on EnC methods is that rsp and rbp are equal
        delta = 0;
    }
    else
    {
        delta = genTotalFrameSize();
    }

#endif // !UNIX_AMD64_ABI

    return delta;
}

//---------------------------------------------------------------------
// genTotalFrameSize - return the total size of the stack frame, including local size,
// callee-saved register size, etc. For AMD64, this does not include the caller-pushed
// return address.
//
// Return value:
//    Total frame size
//

int CodeGenInterface::genTotalFrameSize()
{
    assert(!IsUninitialized(compiler->compCalleeRegsPushed));

    int totalFrameSize = compiler->compCalleeRegsPushed * REGSIZE_BYTES + compiler->compLclFrameSize;

    assert(totalFrameSize >= 0);
    return totalFrameSize;
}

//---------------------------------------------------------------------
// genCallerSPtoFPdelta - return the offset from Caller-SP to the frame pointer.
// This number is going to be negative, since the Caller-SP is at a higher
// address than the frame pointer.
//
// There must be a frame pointer to call this function!
//
// We can't compute this directly from the Caller-SP, since the frame pointer
// is based on a maximum delta from Initial-SP, so first we find SP, then
// compute the FP offset.

int CodeGenInterface::genCallerSPtoFPdelta()
{
    assert(isFramePointerUsed());
    int callerSPtoFPdelta;

    callerSPtoFPdelta = genCallerSPtoInitialSPdelta() + genSPtoFPdelta();

    assert(callerSPtoFPdelta <= 0);
    return callerSPtoFPdelta;
}

//---------------------------------------------------------------------
// genCallerSPtoInitialSPdelta - return the offset from Caller-SP to Initial SP.
//
// This number will be negative.

int CodeGenInterface::genCallerSPtoInitialSPdelta()
{
    int callerSPtoSPdelta = 0;

    callerSPtoSPdelta -= genTotalFrameSize();
    callerSPtoSPdelta -= REGSIZE_BYTES; // caller-pushed return address

    // compCalleeRegsPushed does not account for the frame pointer
    // TODO-Cleanup: shouldn't this be part of genTotalFrameSize?
    if (isFramePointerUsed())
    {
        callerSPtoSPdelta -= REGSIZE_BYTES;
    }

    assert(callerSPtoSPdelta <= 0);
    return callerSPtoSPdelta;
}
#endif // _TARGET_AMD64_

//-----------------------------------------------------------------------------------------
// genSSE2BitwiseOp - generate SSE2 code for the given oper as "Operand BitWiseOp BitMask"
//
// Arguments:
//    treeNode  - tree node
//
// Return value:
//    None
//
// Assumptions:
//     i) tree oper is one of GT_NEG or GT_INTRINSIC Abs()
//    ii) tree type is floating point type.
//   iii) caller of this routine needs to call genProduceReg()
void CodeGen::genSSE2BitwiseOp(GenTreePtr treeNode)
{
    regNumber targetReg  = treeNode->gtRegNum;
    var_types targetType = treeNode->TypeGet();
    assert(varTypeIsFloating(targetType));

    float       f;
    double      d;
    GenTreePtr* bitMask  = nullptr;
    instruction ins      = INS_invalid;
    void*       cnsAddr  = nullptr;
    bool        dblAlign = false;

    switch (treeNode->OperGet())
    {
        case GT_NEG:
            // Neg(x) = flip the sign bit.
            // Neg(f) = f ^ 0x80000000
            // Neg(d) = d ^ 0x8000000000000000
            ins = genGetInsForOper(GT_XOR, targetType);
            if (targetType == TYP_FLOAT)
            {
                bitMask = &negBitmaskFlt;

                static_assert_no_msg(sizeof(float) == sizeof(int));
                *((int*)&f) = 0x80000000;
                cnsAddr     = &f;
            }
            else
            {
                bitMask = &negBitmaskDbl;

                static_assert_no_msg(sizeof(double) == sizeof(__int64));
                *((__int64*)&d) = 0x8000000000000000LL;
                cnsAddr         = &d;
                dblAlign        = true;
            }
            break;

        case GT_INTRINSIC:
            assert(treeNode->gtIntrinsic.gtIntrinsicId == CORINFO_INTRINSIC_Abs);

            // Abs(x) = set sign-bit to zero
            // Abs(f) = f & 0x7fffffff
            // Abs(d) = d & 0x7fffffffffffffff
            ins = genGetInsForOper(GT_AND, targetType);
            if (targetType == TYP_FLOAT)
            {
                bitMask = &absBitmaskFlt;

                static_assert_no_msg(sizeof(float) == sizeof(int));
                *((int*)&f) = 0x7fffffff;
                cnsAddr     = &f;
            }
            else
            {
                bitMask = &absBitmaskDbl;

                static_assert_no_msg(sizeof(double) == sizeof(__int64));
                *((__int64*)&d) = 0x7fffffffffffffffLL;
                cnsAddr         = &d;
                dblAlign        = true;
            }
            break;

        default:
            assert(!"genSSE2: unsupported oper");
            unreached();
            break;
    }

    if (*bitMask == nullptr)
    {
        assert(cnsAddr != nullptr);
        *bitMask = genMakeConst(cnsAddr, targetType, treeNode, dblAlign);
    }

    // We need an additional register for bitmask.
    regNumber tmpReg = treeNode->GetSingleTempReg();

    // Move operand into targetReg only if the reg reserved for
    // internal purpose is not the same as targetReg.
    GenTreePtr op1 = treeNode->gtOp.gtOp1;
    assert(op1->isUsedFromReg());
    regNumber operandReg = genConsumeReg(op1);
    if (tmpReg != targetReg)
    {
        if (operandReg != targetReg)
        {
            inst_RV_RV(ins_Copy(targetType), targetReg, operandReg, targetType);
        }

        operandReg = tmpReg;
    }

    inst_RV_TT(ins_Load(targetType, false), tmpReg, *bitMask);
    assert(ins != INS_invalid);
    inst_RV_RV(ins, targetReg, operandReg, targetType);
}

//---------------------------------------------------------------------
// genIntrinsic - generate code for a given intrinsic
//
// Arguments
//    treeNode - the GT_INTRINSIC node
//
// Return value:
//    None
//
void CodeGen::genIntrinsic(GenTreePtr treeNode)
{
    // Right now only Sqrt/Abs are treated as math intrinsics.
    switch (treeNode->gtIntrinsic.gtIntrinsicId)
    {
        case CORINFO_INTRINSIC_Sqrt:
        {
            // Both operand and its result must be of the same floating point type.
            GenTreePtr srcNode = treeNode->gtOp.gtOp1;
            assert(varTypeIsFloating(srcNode));
            assert(srcNode->TypeGet() == treeNode->TypeGet());

            genConsumeOperands(treeNode->AsOp());
            getEmitter()->emitInsBinary(ins_FloatSqrt(treeNode->TypeGet()), emitTypeSize(treeNode), treeNode, srcNode);
            break;
        }

        case CORINFO_INTRINSIC_Abs:
            genSSE2BitwiseOp(treeNode);
            break;

        default:
            assert(!"genIntrinsic: Unsupported intrinsic");
            unreached();
    }

    genProduceReg(treeNode);
}

//-------------------------------------------------------------------------- //
// getBaseVarForPutArgStk - returns the baseVarNum for passing a stack arg.
//
// Arguments
//    treeNode - the GT_PUTARG_STK node
//
// Return value:
//    The number of the base variable.
//
// Note:
//    If tail call the outgoing args are placed in the caller's incoming arg stack space.
//    Otherwise, they go in the outgoing arg area on the current frame.
//
//    On Windows the caller always creates slots (homing space) in its frame for the
//    first 4 arguments of a callee (register passed args). So, the baseVarNum is always 0.
//    For System V systems there is no such calling convention requirement, and the code needs to find
//    the first stack passed argument from the caller. This is done by iterating over
//    all the lvParam variables and finding the first with lvArgReg equals to REG_STK.
//
unsigned CodeGen::getBaseVarForPutArgStk(GenTreePtr treeNode)
{
    assert(treeNode->OperGet() == GT_PUTARG_STK);

    unsigned baseVarNum;

    // Whether to setup stk arg in incoming or out-going arg area?
    // Fast tail calls implemented as epilog+jmp = stk arg is setup in incoming arg area.
    // All other calls - stk arg is setup in out-going arg area.
    if (treeNode->AsPutArgStk()->putInIncomingArgArea())
    {
        // See the note in the function header re: finding the first stack passed argument.
        baseVarNum = getFirstArgWithStackSlot();
        assert(baseVarNum != BAD_VAR_NUM);

#ifdef DEBUG
        // This must be a fast tail call.
        assert(treeNode->AsPutArgStk()->gtCall->AsCall()->IsFastTailCall());

        // Since it is a fast tail call, the existence of first incoming arg is guaranteed
        // because fast tail call requires that in-coming arg area of caller is >= out-going
        // arg area required for tail call.
        LclVarDsc* varDsc = &(compiler->lvaTable[baseVarNum]);
        assert(varDsc != nullptr);

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING
        assert(!varDsc->lvIsRegArg && varDsc->lvArgReg == REG_STK);
#else  // !FEATURE_UNIX_AMD64_STRUCT_PASSING
        // On Windows this assert is always true. The first argument will always be in REG_ARG_0 or REG_FLTARG_0.
        assert(varDsc->lvIsRegArg && (varDsc->lvArgReg == REG_ARG_0 || varDsc->lvArgReg == REG_FLTARG_0));
#endif // !FEATURE_UNIX_AMD64_STRUCT_PASSING
#endif // !DEBUG
    }
    else
    {
#if FEATURE_FIXED_OUT_ARGS
        baseVarNum = compiler->lvaOutgoingArgSpaceVar;
#else  // !FEATURE_FIXED_OUT_ARGS
        assert(!"No BaseVarForPutArgStk on x86");
        baseVarNum = BAD_VAR_NUM;
#endif // !FEATURE_FIXED_OUT_ARGS
    }

    return baseVarNum;
}

//---------------------------------------------------------------------
// genAlignStackBeforeCall: Align the stack if necessary before a call.
//
// Arguments:
//    putArgStk - the putArgStk node.
//
void CodeGen::genAlignStackBeforeCall(GenTreePutArgStk* putArgStk)
{
#if defined(UNIX_X86_ABI)

    genAlignStackBeforeCall(putArgStk->gtCall);

#endif // UNIX_X86_ABI
}

//---------------------------------------------------------------------
// genAlignStackBeforeCall: Align the stack if necessary before a call.
//
// Arguments:
//    call - the call node.
//
void CodeGen::genAlignStackBeforeCall(GenTreeCall* call)
{
#if defined(UNIX_X86_ABI)

    // Have we aligned the stack yet?
    if (!call->fgArgInfo->IsStkAlignmentDone())
    {
        // We haven't done any stack alignment yet for this call.  We might need to create
        // an alignment adjustment, even if this function itself doesn't have any stack args.
        // This can happen if this function call is part of a nested call sequence, and the outer
        // call has already pushed some arguments.

        unsigned stkLevel = genStackLevel + call->fgArgInfo->GetStkSizeBytes();
        call->fgArgInfo->ComputeStackAlignment(stkLevel);

        unsigned padStkAlign = call->fgArgInfo->GetStkAlign();
        if (padStkAlign != 0)
        {
            // Now generate the alignment
            inst_RV_IV(INS_sub, REG_SPBASE, padStkAlign, EA_PTRSIZE);
            AddStackLevel(padStkAlign);
            AddNestedAlignment(padStkAlign);
        }

        call->fgArgInfo->SetStkAlignmentDone();
    }

#endif // UNIX_X86_ABI
}

//---------------------------------------------------------------------
// genRemoveAlignmentAfterCall: After a call, remove the alignment
// added before the call, if any.
//
// Arguments:
//    call - the call node.
//    bias - additional stack adjustment
//
// Note:
//    When bias > 0, caller should adjust stack level appropriately as
//    bias is not considered when adjusting stack level.
//
void CodeGen::genRemoveAlignmentAfterCall(GenTreeCall* call, unsigned bias)
{
#if defined(_TARGET_X86_)
#if defined(UNIX_X86_ABI)
    // Put back the stack pointer if there was any padding for stack alignment
    unsigned padStkAlign  = call->fgArgInfo->GetStkAlign();
    unsigned padStkAdjust = padStkAlign + bias;

    if (padStkAdjust != 0)
    {
        inst_RV_IV(INS_add, REG_SPBASE, padStkAdjust, EA_PTRSIZE);
        SubtractStackLevel(padStkAlign);
        SubtractNestedAlignment(padStkAlign);
    }
#else  // UNIX_X86_ABI
    if (bias != 0)
    {
        genAdjustSP(bias);
    }
#endif // !UNIX_X86_ABI_
#else  // _TARGET_X86_
    assert(bias == 0);
#endif // !_TARGET_X86
}

#ifdef _TARGET_X86_

//---------------------------------------------------------------------
// genAdjustStackForPutArgStk:
//    adjust the stack pointer for a putArgStk node if necessary.
//
// Arguments:
//    putArgStk - the putArgStk node.
//
// Returns: true if the stack pointer was adjusted; false otherwise.
//
// Notes:
//    Sets `m_pushStkArg` to true if the stack arg needs to be pushed,
//    false if the stack arg needs to be stored at the current stack
//    pointer address. This is exactly the opposite of the return value
//    of this function.
//
bool CodeGen::genAdjustStackForPutArgStk(GenTreePutArgStk* putArgStk)
{
#ifdef FEATURE_SIMD
    if (varTypeIsSIMD(putArgStk))
    {
        const unsigned argSize = genTypeSize(putArgStk);
        inst_RV_IV(INS_sub, REG_SPBASE, argSize, EA_PTRSIZE);
        AddStackLevel(argSize);
        m_pushStkArg = false;
        return true;
    }
#endif // FEATURE_SIMD

    const unsigned argSize = putArgStk->getArgSize();

    // If the gtPutArgStkKind is one of the push types, we do not pre-adjust the stack.
    // This is set in Lowering, and is true if and only if:
    // - This argument contains any GC pointers OR
    // - It is a GT_FIELD_LIST OR
    // - It is less than 16 bytes in size.
    CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef DEBUG
    switch (putArgStk->gtPutArgStkKind)
    {
        case GenTreePutArgStk::Kind::RepInstr:
        case GenTreePutArgStk::Kind::Unroll:
            assert((putArgStk->gtNumberReferenceSlots == 0) && (putArgStk->gtGetOp1()->OperGet() != GT_FIELD_LIST) &&
                   (argSize >= 16));
            break;
        case GenTreePutArgStk::Kind::Push:
        case GenTreePutArgStk::Kind::PushAllSlots:
            assert((putArgStk->gtNumberReferenceSlots != 0) || (putArgStk->gtGetOp1()->OperGet() == GT_FIELD_LIST) ||
                   (argSize < 16));
            break;
        case GenTreePutArgStk::Kind::Invalid:
        default:
            assert(!"Uninitialized GenTreePutArgStk::Kind");
            break;
    }
#endif // DEBUG

    if (putArgStk->isPushKind())
    {
        m_pushStkArg = true;
        return false;
    }
    else
    {
        m_pushStkArg = false;
        inst_RV_IV(INS_sub, REG_SPBASE, argSize, EA_PTRSIZE);
        AddStackLevel(argSize);
        return true;
    }
}

//---------------------------------------------------------------------
// genPutArgStkFieldList - generate code for passing a GT_FIELD_LIST arg on the stack.
//
// Arguments
//    treeNode      - the GT_PUTARG_STK node whose op1 is a GT_FIELD_LIST
//
// Return value:
//    None
//
void CodeGen::genPutArgStkFieldList(GenTreePutArgStk* putArgStk)
{
    GenTreeFieldList* const fieldList = putArgStk->gtOp1->AsFieldList();
    assert(fieldList != nullptr);

    // Set m_pushStkArg and pre-adjust the stack if necessary.
    const bool preAdjustedStack = genAdjustStackForPutArgStk(putArgStk);

    // For now, we only support the "push" case; we will push a full slot for the first field of each slot
    // within the struct.
    assert((putArgStk->isPushKind()) && !preAdjustedStack && m_pushStkArg);

    // If we have pre-adjusted the stack and are simply storing the fields in order, set the offset to 0.
    // (Note that this mode is not currently being used.)
    // If we are pushing the arguments (i.e. we have not pre-adjusted the stack), then we are pushing them
    // in reverse order, so we start with the current field offset at the size of the struct arg (which must be
    // a multiple of the target pointer size).
    unsigned  currentOffset   = (preAdjustedStack) ? 0 : putArgStk->getArgSize();
    unsigned  prevFieldOffset = currentOffset;
    regNumber intTmpReg       = REG_NA;
    regNumber simdTmpReg      = REG_NA;
    if (putArgStk->AvailableTempRegCount() != 0)
    {
        regMaskTP rsvdRegs = putArgStk->gtRsvdRegs;
        if ((rsvdRegs & RBM_ALLINT) != 0)
        {
            intTmpReg = putArgStk->GetSingleTempReg(RBM_ALLINT);
            assert(genIsValidIntReg(intTmpReg));
        }
        if ((rsvdRegs & RBM_ALLFLOAT) != 0)
        {
            simdTmpReg = putArgStk->GetSingleTempReg(RBM_ALLFLOAT);
            assert(genIsValidFloatReg(simdTmpReg));
        }
        assert(genCountBits(rsvdRegs) == (unsigned)((intTmpReg == REG_NA) ? 0 : 1) + ((simdTmpReg == REG_NA) ? 0 : 1));
    }

    for (GenTreeFieldList* current = fieldList; current != nullptr; current = current->Rest())
    {
        GenTree* const fieldNode   = current->Current();
        const unsigned fieldOffset = current->gtFieldOffset;
        var_types      fieldType   = current->gtFieldType;

        // Long-typed nodes should have been handled by the decomposition pass, and lowering should have sorted the
        // field list in descending order by offset.
        assert(!varTypeIsLong(fieldType));
        assert(fieldOffset <= prevFieldOffset);

        // Consume the register, if any, for this field. Note that genConsumeRegs() will appropriately
        // update the liveness info for a lclVar that has been marked RegOptional, which hasn't been
        // assigned a register, and which is therefore contained.
        // Unlike genConsumeReg(), it handles the case where no registers are being consumed.
        genConsumeRegs(fieldNode);
        regNumber argReg = fieldNode->isUsedFromSpillTemp() ? REG_NA : fieldNode->gtRegNum;

        // If the field is slot-like, we can use a push instruction to store the entire register no matter the type.
        //
        // The GC encoder requires that the stack remain 4-byte aligned at all times. Round the adjustment up
        // to the next multiple of 4. If we are going to generate a `push` instruction, the adjustment must
        // not require rounding.
        // NOTE: if the field is of GC type, we must use a push instruction, since the emitter is not otherwise
        // able to detect stores into the outgoing argument area of the stack on x86.
        const bool fieldIsSlot = ((fieldOffset % 4) == 0) && ((prevFieldOffset - fieldOffset) >= 4);
        int        adjustment  = roundUp(currentOffset - fieldOffset, 4);
        if (fieldIsSlot && !varTypeIsSIMD(fieldType))
        {
            fieldType         = genActualType(fieldType);
            unsigned pushSize = genTypeSize(fieldType);
            assert((pushSize % 4) == 0);
            adjustment -= pushSize;
            while (adjustment != 0)
            {
                inst_IV(INS_push, 0);
                currentOffset -= pushSize;
                AddStackLevel(pushSize);
                adjustment -= pushSize;
            }
            m_pushStkArg = true;
        }
        else
        {
            m_pushStkArg = false;

            // We always "push" floating point fields (i.e. they are full slot values that don't
            // require special handling).
            assert(varTypeIsIntegralOrI(fieldNode) || varTypeIsSIMD(fieldNode));

            // If we can't push this field, it needs to be in a register so that we can store
            // it to the stack location.
            if (adjustment != 0)
            {
                // This moves the stack pointer to fieldOffset.
                // For this case, we must adjust the stack and generate stack-relative stores rather than pushes.
                // Adjust the stack pointer to the next slot boundary.
                inst_RV_IV(INS_sub, REG_SPBASE, adjustment, EA_PTRSIZE);
                currentOffset -= adjustment;
                AddStackLevel(adjustment);
            }

            // Does it need to be in a byte register?
            // If so, we'll use intTmpReg, which must have been allocated as a byte register.
            // If it's already in a register, but not a byteable one, then move it.
            if (varTypeIsByte(fieldType) && ((argReg == REG_NA) || ((genRegMask(argReg) & RBM_BYTE_REGS) == 0)))
            {
                assert(intTmpReg != REG_NA);
                noway_assert((genRegMask(intTmpReg) & RBM_BYTE_REGS) != 0);
                if (argReg != REG_NA)
                {
                    inst_RV_RV(INS_mov, intTmpReg, argReg, fieldType);
                    argReg = intTmpReg;
                }
            }
        }

        if (argReg == REG_NA)
        {
            if (m_pushStkArg)
            {
                if (fieldNode->isUsedFromSpillTemp())
                {
                    assert(!varTypeIsSIMD(fieldType)); // Q: can we get here with SIMD?
                    assert(fieldNode->IsRegOptional());
                    TempDsc* tmp = getSpillTempDsc(fieldNode);
                    getEmitter()->emitIns_S(INS_push, emitActualTypeSize(fieldNode->TypeGet()), tmp->tdTempNum(), 0);
                    compiler->tmpRlsTemp(tmp);
                }
                else
                {
                    assert(varTypeIsIntegralOrI(fieldNode));
                    switch (fieldNode->OperGet())
                    {
                        case GT_LCL_VAR:
                            inst_TT(INS_push, fieldNode, 0, 0, emitActualTypeSize(fieldNode->TypeGet()));
                            break;
                        case GT_CNS_INT:
                            if (fieldNode->IsIconHandle())
                            {
                                inst_IV_handle(INS_push, fieldNode->gtIntCon.gtIconVal);
                            }
                            else
                            {
                                inst_IV(INS_push, fieldNode->gtIntCon.gtIconVal);
                            }
                            break;
                        default:
                            unreached();
                    }
                }
                currentOffset -= TARGET_POINTER_SIZE;
                AddStackLevel(TARGET_POINTER_SIZE);
            }
            else
            {
                // The stack has been adjusted and we will load the field to intTmpReg and then store it on the stack.
                assert(varTypeIsIntegralOrI(fieldNode));
                switch (fieldNode->OperGet())
                {
                    case GT_LCL_VAR:
                        inst_RV_TT(INS_mov, intTmpReg, fieldNode);
                        break;
                    case GT_CNS_INT:
                        genSetRegToConst(intTmpReg, fieldNode->TypeGet(), fieldNode);
                        break;
                    default:
                        unreached();
                }
                genStoreRegToStackArg(fieldType, intTmpReg, fieldOffset - currentOffset);
            }
        }
        else
        {
#if defined(FEATURE_SIMD)
            if (fieldType == TYP_SIMD12)
            {
                assert(genIsValidFloatReg(simdTmpReg));
                genStoreSIMD12ToStack(argReg, simdTmpReg);
            }
            else
#endif // defined(FEATURE_SIMD)
            {
                genStoreRegToStackArg(fieldType, argReg, fieldOffset - currentOffset);
            }
            if (m_pushStkArg)
            {
                // We always push a slot-rounded size
                currentOffset -= genTypeSize(fieldType);
            }
        }

        prevFieldOffset = fieldOffset;
    }
    if (currentOffset != 0)
    {
        // We don't expect padding at the beginning of a struct, but it could happen with explicit layout.
        inst_RV_IV(INS_sub, REG_SPBASE, currentOffset, EA_PTRSIZE);
        AddStackLevel(currentOffset);
    }
}
#endif // _TARGET_X86_

//---------------------------------------------------------------------
// genPutArgStk - generate code for passing an arg on the stack.
//
// Arguments
//    treeNode      - the GT_PUTARG_STK node
//    targetType    - the type of the treeNode
//
// Return value:
//    None
//
void CodeGen::genPutArgStk(GenTreePutArgStk* putArgStk)
{
    var_types targetType = putArgStk->TypeGet();

#ifdef _TARGET_X86_

    genAlignStackBeforeCall(putArgStk);

    if (varTypeIsStruct(targetType))
    {
        (void)genAdjustStackForPutArgStk(putArgStk);
        genPutStructArgStk(putArgStk);
        return;
    }

    // The following logic is applicable for x86 arch.
    assert(!varTypeIsFloating(targetType) || (targetType == putArgStk->gtOp1->TypeGet()));

    GenTreePtr data = putArgStk->gtOp1;

    // On a 32-bit target, all of the long arguments are handled with GT_FIELD_LIST,
    // and the type of the putArgStk is TYP_VOID.
    assert(targetType != TYP_LONG);

    const unsigned argSize = putArgStk->getArgSize();
    assert((argSize % TARGET_POINTER_SIZE) == 0);

    if (data->isContainedIntOrIImmed())
    {
        if (data->IsIconHandle())
        {
            inst_IV_handle(INS_push, data->gtIntCon.gtIconVal);
        }
        else
        {
            inst_IV(INS_push, data->gtIntCon.gtIconVal);
        }
        AddStackLevel(argSize);
    }
    else if (data->OperGet() == GT_FIELD_LIST)
    {
        genPutArgStkFieldList(putArgStk);
    }
    else
    {
        // We should not see any contained nodes that are not immediates.
        assert(data->isUsedFromReg());
        genConsumeReg(data);
        genPushReg(targetType, data->gtRegNum);
    }
#else // !_TARGET_X86_
    {
        unsigned baseVarNum = getBaseVarForPutArgStk(putArgStk);

#ifdef FEATURE_UNIX_AMD64_STRUCT_PASSING

        if (varTypeIsStruct(targetType))
        {
            m_stkArgVarNum = baseVarNum;
            m_stkArgOffset = putArgStk->getArgOffset();
            genPutStructArgStk(putArgStk);
            m_stkArgVarNum = BAD_VAR_NUM;
            return;
        }
#endif // FEATURE_UNIX_AMD64_STRUCT_PASSING

        noway_assert(targetType != TYP_STRUCT);
        assert(!varTypeIsFloating(targetType) || (targetType == putArgStk->gtOp1->TypeGet()));

        // Get argument offset on stack.
        // Here we cross check that argument offset hasn't changed from lowering to codegen since
        // we are storing arg slot number in GT_PUTARG_STK node in lowering phase.
        int              argOffset      = putArgStk->getArgOffset();

#ifdef DEBUG
        fgArgTabEntryPtr curArgTabEntry = compiler->gtArgEntryByNode(putArgStk->gtCall, putArgStk);
        assert(curArgTabEntry);
        assert(argOffset == (int)curArgTabEntry->slotNum * TARGET_POINTER_SIZE);
#endif

        GenTreePtr data = putArgStk->gtOp1;

        if (data->isContainedIntOrIImmed())
        {
            getEmitter()->emitIns_S_I(ins_Store(targetType), emitTypeSize(targetType), baseVarNum, argOffset,
                                      (int)data->AsIntConCommon()->IconValue());
        }
        else
        {
            assert(data->isUsedFromReg());
            genConsumeReg(data);
            getEmitter()->emitIns_S_R(ins_Store(targetType), emitTypeSize(targetType), data->gtRegNum, baseVarNum,
                                      argOffset);
        }
    }
#endif // !_TARGET_X86_
}

#ifdef _TARGET_X86_
// genPushReg: Push a register value onto the stack and adjust the stack level
//
// Arguments:
//    type   - the type of value to be stored
//    reg    - the register containing the value
//
// Notes:
//    For TYP_LONG, the srcReg must be a floating point register.
//    Otherwise, the register type must be consistent with the given type.
//
void CodeGen::genPushReg(var_types type, regNumber srcReg)
{
    unsigned size = genTypeSize(type);
    if (varTypeIsIntegralOrI(type) && type != TYP_LONG)
    {
        assert(genIsValidIntReg(srcReg));
        inst_RV(INS_push, srcReg, type);
    }
    else
    {
        instruction ins;
        emitAttr    attr = emitTypeSize(type);
        if (type == TYP_LONG)
        {
            // On x86, the only way we can push a TYP_LONG from a register is if it is in an xmm reg.
            // This is only used when we are pushing a struct from memory to memory, and basically is
            // handling an 8-byte "chunk", as opposed to strictly a long type.
            ins = INS_movq;
        }
        else
        {
            ins = ins_Store(type);
        }
        assert(genIsValidFloatReg(srcReg));
        inst_RV_IV(INS_sub, REG_SPBASE, size, EA_PTRSIZE);
        getEmitter()->emitIns_AR_R(ins, attr, srcReg, REG_SPBASE, 0);
    }
    AddStackLevel(size);
}
#endif // _TARGET_X86_

#if defined(FEATURE_PUT_STRUCT_ARG_STK)
// genStoreRegToStackArg: Store a register value into the stack argument area
//
// Arguments:
//    type   - the type of value to be stored
//    reg    - the register containing the value
//    offset - the offset from the base (see Assumptions below)
//
// Notes:
//    A type of TYP_STRUCT instructs this method to store a 16-byte chunk
//    at the given offset (i.e. not the full struct).
//
// Assumptions:
//    The caller must set the context appropriately before calling this method:
//    - On x64, m_stkArgVarNum must be set according to whether this is a regular or tail call.
//    - On x86, the caller must set m_pushStkArg if this method should push the argument.
//      Otherwise, the argument is stored at the given offset from sp.
//
// TODO: In the below code the load and store instructions are for 16 bytes, but the
//          type is EA_8BYTE. The movdqa/u are 16 byte instructions, so it works, but
//          this probably needs to be changed.
//
void CodeGen::genStoreRegToStackArg(var_types type, regNumber srcReg, int offset)
{
    assert(srcReg != REG_NA);
    instruction ins;
    emitAttr    attr;
    unsigned    size;

    if (type == TYP_STRUCT)
    {
        ins = INS_movdqu;
        // This should be changed!
        attr = EA_8BYTE;
        size = 16;
    }
    else
    {
#ifdef FEATURE_SIMD
        if (varTypeIsSIMD(type))
        {
            assert(genIsValidFloatReg(srcReg));
            ins = ins_Store(type); // TODO-CQ: pass 'aligned' correctly
        }
        else
#endif // FEATURE_SIMD
#ifdef _TARGET_X86_
            if (type == TYP_LONG)
        {
            assert(genIsValidFloatReg(srcReg));
            ins = INS_movq;
        }
        else
#endif // _TARGET_X86_
        {
            assert((varTypeIsFloating(type) && genIsValidFloatReg(srcReg)) ||
                   (varTypeIsIntegralOrI(type) && genIsValidIntReg(srcReg)));
            ins = ins_Store(type);
        }
        attr = emitTypeSize(type);
        size = genTypeSize(type);
    }

#ifdef _TARGET_X86_
    if (m_pushStkArg)
    {
        genPushReg(type, srcReg);
    }
    else
    {
        getEmitter()->emitIns_AR_R(ins, attr, srcReg, REG_SPBASE, offset);
    }
#else  // !_TARGET_X86_
    assert(m_stkArgVarNum != BAD_VAR_NUM);
    getEmitter()->emitIns_S_R(ins, attr, srcReg, m_stkArgVarNum, m_stkArgOffset + offset);
#endif // !_TARGET_X86_
}

//---------------------------------------------------------------------
// genPutStructArgStk - generate code for copying a struct arg on the stack by value.
//                In case there are references to heap object in the struct,
//                it generates the gcinfo as well.
//
// Arguments
//    putArgStk - the GT_PUTARG_STK node
//
// Notes:
//    In the case of fixed out args, the caller must have set m_stkArgVarNum to the variable number
//    corresponding to the argument area (where we will put the argument on the stack).
//    For tail calls this is the baseVarNum = 0.
//    For non tail calls this is the outgoingArgSpace.
void CodeGen::genPutStructArgStk(GenTreePutArgStk* putArgStk)
{
    var_types targetType = putArgStk->TypeGet();

#if defined(_TARGET_X86_) && defined(FEATURE_SIMD)
    if (targetType == TYP_SIMD12)
    {
        genPutArgStkSIMD12(putArgStk);
        return;
    }
#endif // defined(_TARGET_X86_) && defined(FEATURE_SIMD)

    if (varTypeIsSIMD(targetType))
    {
        regNumber srcReg = genConsumeReg(putArgStk->gtGetOp1());
        assert((srcReg != REG_NA) && (genIsValidFloatReg(srcReg)));
        genStoreRegToStackArg(targetType, srcReg, 0);
        return;
    }

    assert(targetType == TYP_STRUCT);

    if (putArgStk->gtNumberReferenceSlots == 0)
    {
        switch (putArgStk->gtPutArgStkKind)
        {
            case GenTreePutArgStk::Kind::RepInstr:
                genStructPutArgRepMovs(putArgStk);
                break;
            case GenTreePutArgStk::Kind::Unroll:
                genStructPutArgUnroll(putArgStk);
                break;
            case GenTreePutArgStk::Kind::Push:
                genStructPutArgUnroll(putArgStk);
                break;
            default:
                unreached();
        }
    }
    else
    {
        // No need to disable GC the way COPYOBJ does. Here the refs are copied in atomic operations always.
        CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef _TARGET_X86_
        // On x86, any struct that has contains GC references must be stored to the stack using `push` instructions so
        // that the emitter properly detects the need to update the method's GC information.
        //
        // Strictly speaking, it is only necessary to use `push` to store the GC references themselves, so for structs
        // with large numbers of consecutive non-GC-ref-typed fields, we may be able to improve the code size in the
        // future.
        assert(m_pushStkArg);

        GenTree*       srcAddr  = putArgStk->gtGetOp1()->gtGetOp1();
        BYTE*          gcPtrs   = putArgStk->gtGcPtrs;
        const unsigned numSlots = putArgStk->gtNumSlots;

        regNumber  srcRegNum    = srcAddr->gtRegNum;
        const bool srcAddrInReg = srcRegNum != REG_NA;

        unsigned srcLclNum    = 0;
        unsigned srcLclOffset = 0;
        if (srcAddrInReg)
        {
            genConsumeReg(srcAddr);
        }
        else
        {
            assert(srcAddr->OperIsLocalAddr());

            srcLclNum = srcAddr->AsLclVarCommon()->gtLclNum;
            if (srcAddr->OperGet() == GT_LCL_FLD_ADDR)
            {
                srcLclOffset = srcAddr->AsLclFld()->gtLclOffs;
            }
        }

        for (int i = numSlots - 1; i >= 0; --i)
        {
            emitAttr slotAttr;
            if (gcPtrs[i] == TYPE_GC_NONE)
            {
                slotAttr = EA_4BYTE;
            }
            else if (gcPtrs[i] == TYPE_GC_REF)
            {
                slotAttr = EA_GCREF;
            }
            else
            {
                assert(gcPtrs[i] == TYPE_GC_BYREF);
                slotAttr = EA_BYREF;
            }

            const unsigned offset = i * TARGET_POINTER_SIZE;
            if (srcAddrInReg)
            {
                getEmitter()->emitIns_AR_R(INS_push, slotAttr, REG_NA, srcRegNum, offset);
            }
            else
            {
                getEmitter()->emitIns_S(INS_push, slotAttr, srcLclNum, srcLclOffset + offset);
            }
            AddStackLevel(TARGET_POINTER_SIZE);
        }
#else // !defined(_TARGET_X86_)

        // Consume these registers.
        // They may now contain gc pointers (depending on their type; gcMarkRegPtrVal will "do the right thing").
        genConsumePutStructArgStk(putArgStk, REG_RDI, REG_RSI, REG_NA);

        const bool     srcIsLocal       = putArgStk->gtOp1->AsObj()->gtOp1->OperIsLocalAddr();
        const emitAttr srcAddrAttr      = srcIsLocal ? EA_PTRSIZE : EA_BYREF;

#if DEBUG
        unsigned       numGCSlotsCopied = 0;
#endif // DEBUG

        BYTE*          gcPtrs   = putArgStk->gtGcPtrs;
        const unsigned numSlots = putArgStk->gtNumSlots;
        for (unsigned i = 0; i < numSlots;)
        {
            if (gcPtrs[i] == TYPE_GC_NONE)
            {
                // Let's see if we can use rep movsp (alias for movsd or movsq for 32 and 64 bits respectively)
                // instead of a sequence of movsp instructions to save cycles and code size.
                unsigned adjacentNonGCSlotCount = 0;
                do
                {
                    adjacentNonGCSlotCount++;
                    i++;
                } while ((i < numSlots) && (gcPtrs[i] == TYPE_GC_NONE));

                // If we have a very small contiguous non-ref region, it's better just to
                // emit a sequence of movsp instructions
                if (adjacentNonGCSlotCount < CPOBJ_NONGC_SLOTS_LIMIT)
                {
                    for (; adjacentNonGCSlotCount > 0; adjacentNonGCSlotCount--)
                    {
                        instGen(INS_movsp);
                    }
                }
                else
                {
                    getEmitter()->emitIns_R_I(INS_mov, EA_4BYTE, REG_RCX, adjacentNonGCSlotCount);
                    instGen(INS_r_movsp);
                }
            }
            else
            {
                assert((gcPtrs[i] == TYPE_GC_REF) || (gcPtrs[i] == TYPE_GC_BYREF));

                // We have a GC (byref or ref) pointer
                // TODO-Amd64-Unix: Here a better solution (for code size and CQ) would be to use movsp instruction,
                // but the logic for emitting a GC info record is not available (it is internal for the emitter
                // only.) See emitGCVarLiveUpd function. If we could call it separately, we could do
                // instGen(INS_movsp); and emission of gc info.

                var_types memType = (gcPtrs[i] == TYPE_GC_REF) ? TYP_REF : TYP_BYREF;
                getEmitter()->emitIns_R_AR(ins_Load(memType), emitTypeSize(memType), REG_RCX, REG_RSI, 0);
                genStoreRegToStackArg(memType, REG_RCX, i * TARGET_POINTER_SIZE);

#ifdef DEBUG
                numGCSlotsCopied++;
#endif // DEBUG

                i++;
                if (i < numSlots)
                {
                    // Source for the copy operation.
                    // If a LocalAddr, use EA_PTRSIZE - copy from stack.
                    // If not a LocalAddr, use EA_BYREF - the source location is not on the stack.
                    getEmitter()->emitIns_R_I(INS_add, srcAddrAttr, REG_RSI, TARGET_POINTER_SIZE);

                    // Always copying to the stack - outgoing arg area
                    // (or the outgoing arg area of the caller for a tail call) - use EA_PTRSIZE.
                    getEmitter()->emitIns_R_I(INS_add, EA_PTRSIZE, REG_RDI, TARGET_POINTER_SIZE);
                }
            }
        }

        assert(numGCSlotsCopied == putArgStk->gtNumberReferenceSlots);
#endif // _TARGET_X86_
    }
}
#endif // defined(FEATURE_PUT_STRUCT_ARG_STK)

/*****************************************************************************
 *
 *  Create and record GC Info for the function.
 */
#ifndef JIT32_GCENCODER
void
#else  // !JIT32_GCENCODER
void*
#endif // !JIT32_GCENCODER
CodeGen::genCreateAndStoreGCInfo(unsigned codeSize, unsigned prologSize, unsigned epilogSize DEBUGARG(void* codePtr))
{
#ifdef JIT32_GCENCODER
    return genCreateAndStoreGCInfoJIT32(codeSize, prologSize, epilogSize DEBUGARG(codePtr));
#else  // !JIT32_GCENCODER
    genCreateAndStoreGCInfoX64(codeSize, prologSize DEBUGARG(codePtr));
#endif // !JIT32_GCENCODER
}

#ifdef JIT32_GCENCODER
void* CodeGen::genCreateAndStoreGCInfoJIT32(unsigned codeSize,
                                            unsigned prologSize,
                                            unsigned epilogSize DEBUGARG(void* codePtr))
{
    BYTE    headerBuf[64];
    InfoHdr header;

    int s_cached;
#ifdef DEBUG
    size_t headerSize =
#endif
        compiler->compInfoBlkSize =
            gcInfo.gcInfoBlockHdrSave(headerBuf, 0, codeSize, prologSize, epilogSize, &header, &s_cached);

    size_t argTabOffset = 0;
    size_t ptrMapSize   = gcInfo.gcPtrTableSize(header, codeSize, &argTabOffset);

#if DISPLAY_SIZES

    if (genInterruptible)
    {
        gcHeaderISize += compiler->compInfoBlkSize;
        gcPtrMapISize += ptrMapSize;
    }
    else
    {
        gcHeaderNSize += compiler->compInfoBlkSize;
        gcPtrMapNSize += ptrMapSize;
    }

#endif // DISPLAY_SIZES

    compiler->compInfoBlkSize += ptrMapSize;

    /* Allocate the info block for the method */

    compiler->compInfoBlkAddr = (BYTE*)compiler->info.compCompHnd->allocGCInfo(compiler->compInfoBlkSize);

#if 0 // VERBOSE_SIZES
    // TODO-X86-Cleanup: 'dataSize', below, is not defined

//  if  (compiler->compInfoBlkSize > codeSize && compiler->compInfoBlkSize > 100)
    {
        printf("[%7u VM, %7u+%7u/%7u x86 %03u/%03u%%] %s.%s\n",
               compiler->info.compILCodeSize,
               compiler->compInfoBlkSize,
               codeSize + dataSize,
               codeSize + dataSize - prologSize - epilogSize,
               100 * (codeSize + dataSize) / compiler->info.compILCodeSize,
               100 * (codeSize + dataSize + compiler->compInfoBlkSize) / compiler->info.compILCodeSize,
               compiler->info.compClassName,
               compiler->info.compMethodName);
}

#endif

    /* Fill in the info block and return it to the caller */

    void* infoPtr = compiler->compInfoBlkAddr;

    /* Create the method info block: header followed by GC tracking tables */

    compiler->compInfoBlkAddr +=
        gcInfo.gcInfoBlockHdrSave(compiler->compInfoBlkAddr, -1, codeSize, prologSize, epilogSize, &header, &s_cached);

    assert(compiler->compInfoBlkAddr == (BYTE*)infoPtr + headerSize);
    compiler->compInfoBlkAddr = gcInfo.gcPtrTableSave(compiler->compInfoBlkAddr, header, codeSize, &argTabOffset);
    assert(compiler->compInfoBlkAddr == (BYTE*)infoPtr + headerSize + ptrMapSize);

#ifdef DEBUG

    if (0)
    {
        BYTE*    temp = (BYTE*)infoPtr;
        unsigned size = compiler->compInfoBlkAddr - temp;
        BYTE*    ptab = temp + headerSize;

        noway_assert(size == headerSize + ptrMapSize);

        printf("Method info block - header [%u bytes]:", headerSize);

        for (unsigned i = 0; i < size; i++)
        {
            if (temp == ptab)
            {
                printf("\nMethod info block - ptrtab [%u bytes]:", ptrMapSize);
                printf("\n    %04X: %*c", i & ~0xF, 3 * (i & 0xF), ' ');
            }
            else
            {
                if (!(i % 16))
                    printf("\n    %04X: ", i);
            }

            printf("%02X ", *temp++);
        }

        printf("\n");
    }

#endif // DEBUG

#if DUMP_GC_TABLES

    if (compiler->opts.dspGCtbls)
    {
        const BYTE* base = (BYTE*)infoPtr;
        unsigned    size;
        unsigned    methodSize;
        InfoHdr     dumpHeader;

        printf("GC Info for method %s\n", compiler->info.compFullName);
        printf("GC info size = %3u\n", compiler->compInfoBlkSize);

        size = gcInfo.gcInfoBlockHdrDump(base, &dumpHeader, &methodSize);
        // printf("size of header encoding is %3u\n", size);
        printf("\n");

        if (compiler->opts.dspGCtbls)
        {
            base += size;
            size = gcInfo.gcDumpPtrTable(base, dumpHeader, methodSize);
            // printf("size of pointer table is %3u\n", size);
            printf("\n");
            noway_assert(compiler->compInfoBlkAddr == (base + size));
        }
    }

#ifdef DEBUG
    if (jitOpts.testMask & 128)
    {
        for (unsigned offs = 0; offs < codeSize; offs++)
        {
            gcInfo.gcFindPtrsInFrame(infoPtr, codePtr, offs);
        }
    }
#endif // DEBUG
#endif // DUMP_GC_TABLES

    /* Make sure we ended up generating the expected number of bytes */

    noway_assert(compiler->compInfoBlkAddr == (BYTE*)infoPtr + compiler->compInfoBlkSize);

    return infoPtr;
}

#else  // !JIT32_GCENCODER
void CodeGen::genCreateAndStoreGCInfoX64(unsigned codeSize, unsigned prologSize DEBUGARG(void* codePtr))
{
    IAllocator*    allowZeroAlloc = new (compiler, CMK_GC) AllowZeroAllocator(compiler->getAllocatorGC());
    GcInfoEncoder* gcInfoEncoder  = new (compiler, CMK_GC)
        GcInfoEncoder(compiler->info.compCompHnd, compiler->info.compMethodInfo, allowZeroAlloc, NOMEM);
    assert(gcInfoEncoder);

    // Follow the code pattern of the x86 gc info encoder (genCreateAndStoreGCInfoJIT32).
    gcInfo.gcInfoBlockHdrSave(gcInfoEncoder, codeSize, prologSize);

    // We keep the call count for the second call to gcMakeRegPtrTable() below.
    unsigned callCnt = 0;
    // First we figure out the encoder ID's for the stack slots and registers.
    gcInfo.gcMakeRegPtrTable(gcInfoEncoder, codeSize, prologSize, GCInfo::MAKE_REG_PTR_MODE_ASSIGN_SLOTS, &callCnt);
    // Now we've requested all the slots we'll need; "finalize" these (make more compact data structures for them).
    gcInfoEncoder->FinalizeSlotIds();
    // Now we can actually use those slot ID's to declare live ranges.
    gcInfo.gcMakeRegPtrTable(gcInfoEncoder, codeSize, prologSize, GCInfo::MAKE_REG_PTR_MODE_DO_WORK, &callCnt);

    if (compiler->opts.compDbgEnC)
    {
        // what we have to preserve is called the "frame header" (see comments in VM\eetwain.cpp)
        // which is:
        //  -return address
        //  -saved off RBP
        //  -saved 'this' pointer and bool for synchronized methods

        // 4 slots for RBP + return address + RSI + RDI
        int preservedAreaSize = 4 * REGSIZE_BYTES;

        if (compiler->info.compFlags & CORINFO_FLG_SYNCH)
        {
            if (!(compiler->info.compFlags & CORINFO_FLG_STATIC))
            {
                preservedAreaSize += REGSIZE_BYTES;
            }

            // bool in synchronized methods that tracks whether the lock has been taken (takes 4 bytes on stack)
            preservedAreaSize += 4;
        }

        // Used to signal both that the method is compiled for EnC, and also the size of the block at the top of the
        // frame
        gcInfoEncoder->SetSizeOfEditAndContinuePreservedArea(preservedAreaSize);
    }

    if (compiler->opts.IsReversePInvoke())
    {
        unsigned reversePInvokeFrameVarNumber = compiler->lvaReversePInvokeFrameVar;
        assert(reversePInvokeFrameVarNumber != BAD_VAR_NUM && reversePInvokeFrameVarNumber < compiler->lvaRefCount);
        LclVarDsc& reversePInvokeFrameVar = compiler->lvaTable[reversePInvokeFrameVarNumber];
        gcInfoEncoder->SetReversePInvokeFrameSlot(reversePInvokeFrameVar.lvStkOffs);
    }

    gcInfoEncoder->Build();

    // GC Encoder automatically puts the GC info in the right spot using ICorJitInfo::allocGCInfo(size_t)
    // let's save the values anyway for debugging purposes
    compiler->compInfoBlkAddr = gcInfoEncoder->Emit();
    compiler->compInfoBlkSize = 0; // not exposed by the GCEncoder interface
}
#endif // !JIT32_GCENCODER

/*****************************************************************************
 *  Emit a call to a helper function.
 *
 */

void CodeGen::genEmitHelperCall(unsigned helper, int argSize, emitAttr retSize, regNumber callTargetReg)
{
    void* addr  = nullptr;
    void* pAddr = nullptr;

    emitter::EmitCallType callType = emitter::EC_FUNC_TOKEN;
    addr                           = compiler->compGetHelperFtn((CorInfoHelpFunc)helper, &pAddr);
    regNumber callTarget           = REG_NA;
    regMaskTP killMask             = compiler->compHelperCallKillSet((CorInfoHelpFunc)helper);

    if (!addr)
    {
        assert(pAddr != nullptr);

        // Absolute indirect call addr
        // Note: Order of checks is important. First always check for pc-relative and next
        // zero-relative.  Because the former encoding is 1-byte smaller than the latter.
        if (genCodeIndirAddrCanBeEncodedAsPCRelOffset((size_t)pAddr) ||
            genCodeIndirAddrCanBeEncodedAsZeroRelOffset((size_t)pAddr))
        {
            // generate call whose target is specified by 32-bit offset relative to PC or zero.
            callType = emitter::EC_FUNC_TOKEN_INDIR;
            addr     = pAddr;
        }
        else
        {
#ifdef _TARGET_AMD64_
            // If this indirect address cannot be encoded as 32-bit offset relative to PC or Zero,
            // load it into REG_HELPER_CALL_TARGET and use register indirect addressing mode to
            // make the call.
            //    mov   reg, addr
            //    call  [reg]

            if (callTargetReg == REG_NA)
            {
                // If a callTargetReg has not been explicitly provided, we will use REG_DEFAULT_HELPER_CALL_TARGET, but
                // this is only a valid assumption if the helper call is known to kill REG_DEFAULT_HELPER_CALL_TARGET.
                callTargetReg            = REG_DEFAULT_HELPER_CALL_TARGET;
                regMaskTP callTargetMask = genRegMask(callTargetReg);
                noway_assert((callTargetMask & killMask) == callTargetMask);
            }
            else
            {
                // The call target must not overwrite any live variable, though it may not be in the
                // kill set for the call.
                regMaskTP callTargetMask = genRegMask(callTargetReg);
                noway_assert((callTargetMask & regSet.rsMaskVars) == RBM_NONE);
            }
#endif

            callTarget = callTargetReg;
            CodeGen::genSetRegToIcon(callTarget, (ssize_t)pAddr, TYP_I_IMPL);
            callType = emitter::EC_INDIR_ARD;
        }
    }

    // clang-format off
    getEmitter()->emitIns_Call(callType,
                               compiler->eeFindHelper(helper),
                               INDEBUG_LDISASM_COMMA(nullptr) addr,
                               argSize,
                               retSize
                               MULTIREG_HAS_SECOND_GC_RET_ONLY_ARG(EA_UNKNOWN),
                               gcInfo.gcVarPtrSetCur,
                               gcInfo.gcRegGCrefSetCur,
                               gcInfo.gcRegByrefSetCur,
                               BAD_IL_OFFSET, // IL offset
                               callTarget,    // ireg
                               REG_NA, 0, 0,  // xreg, xmul, disp
                               false,         // isJump
                               emitter::emitNoGChelper(helper));
    // clang-format on

    regTracker.rsTrashRegSet(killMask);
    regTracker.rsTrashRegsForGCInterruptability();
}

#if !defined(_TARGET_64BIT_)
//-----------------------------------------------------------------------------
//
// Code Generation for Long integers
//
//-----------------------------------------------------------------------------

//------------------------------------------------------------------------
// genStoreLongLclVar: Generate code to store a non-enregistered long lclVar
//
// Arguments:
//    treeNode - A TYP_LONG lclVar node.
//
// Return Value:
//    None.
//
// Assumptions:
//    'treeNode' must be a TYP_LONG lclVar node for a lclVar that has NOT been promoted.
//    Its operand must be a GT_LONG node.
//
void CodeGen::genStoreLongLclVar(GenTree* treeNode)
{
    emitter* emit = getEmitter();

    GenTreeLclVarCommon* lclNode = treeNode->AsLclVarCommon();
    unsigned             lclNum  = lclNode->gtLclNum;
    LclVarDsc*           varDsc  = &(compiler->lvaTable[lclNum]);
    assert(varDsc->TypeGet() == TYP_LONG);
    assert(!varDsc->lvPromoted);
    GenTreePtr op1 = treeNode->gtOp.gtOp1;
    noway_assert(op1->OperGet() == GT_LONG || op1->OperGet() == GT_MUL_LONG);
    genConsumeRegs(op1);

    if (op1->OperGet() == GT_LONG)
    {
        // Definitions of register candidates will have been lowered to 2 int lclVars.
        assert(!treeNode->InReg());

        GenTreePtr loVal = op1->gtGetOp1();
        GenTreePtr hiVal = op1->gtGetOp2();

        // NYI: Contained immediates.
        NYI_IF((loVal->gtRegNum == REG_NA) || (hiVal->gtRegNum == REG_NA),
               "Store of long lclVar with contained immediate");

        emit->emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, loVal->gtRegNum, lclNum, 0);
        emit->emitIns_S_R(ins_Store(TYP_INT), EA_4BYTE, hiVal->gtRegNum, lclNum, genTypeSize(TYP_INT));
    }
    else if (op1->OperGet() == GT_MUL_LONG)
    {
        assert((op1->gtFlags & GTF_MUL_64RSLT) != 0);

        // Stack store
        getEmitter()->emitIns_S_R(ins_Store(TYP_INT), emitTypeSize(TYP_INT), REG_LNGRET_LO, lclNum, 0);
        getEmitter()->emitIns_S_R(ins_Store(TYP_INT), emitTypeSize(TYP_INT), REG_LNGRET_HI, lclNum,
                                  genTypeSize(TYP_INT));
    }
}
#endif // !defined(_TARGET_64BIT_)

/*****************************************************************************
* Unit testing of the XArch emitter: generate a bunch of instructions into the prolog
* (it's as good a place as any), then use COMPlus_JitLateDisasm=* to see if the late
* disassembler thinks the instructions as the same as we do.
*/

// Uncomment "#define ALL_ARM64_EMITTER_UNIT_TESTS" to run all the unit tests here.
// After adding a unit test, and verifying it works, put it under this #ifdef, so we don't see it run every time.
//#define ALL_XARCH_EMITTER_UNIT_TESTS

#if defined(DEBUG) && defined(LATE_DISASM) && defined(_TARGET_AMD64_)
void CodeGen::genAmd64EmitterUnitTests()
{
    if (!verbose)
    {
        return;
    }

    if (!compiler->opts.altJit)
    {
        // No point doing this in a "real" JIT.
        return;
    }

    // Mark the "fake" instructions in the output.
    printf("*************** In genAmd64EmitterUnitTests()\n");

    // We use this:
    //      genDefineTempLabel(genCreateTempLabel());
    // to create artificial labels to help separate groups of tests.

    //
    // Loads
    //
    CLANG_FORMAT_COMMENT_ANCHOR;

#ifdef ALL_XARCH_EMITTER_UNIT_TESTS
#ifdef FEATURE_AVX_SUPPORT
    genDefineTempLabel(genCreateTempLabel());

    // vhaddpd     ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_haddpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_addss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_addsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddps      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_addps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddpd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_addpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vaddpd      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_addpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_subss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_subsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_subps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_subps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubpd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_subpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vsubpd      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_subpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_mulss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_mulsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulps      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_mulps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulpd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_mulpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_mulps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vmulpd      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_mulpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vandps      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_andps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vandpd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_andpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vandps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_andps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vandpd      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_andpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vorps      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_orps, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vorpd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_orpd, EA_16BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vorps      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_orps, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vorpd      ymm0,ymm1,ymm2
    getEmitter()->emitIns_R_R_R(INS_orpd, EA_32BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vdivss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_divss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vdivsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_divsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vdivss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_divss, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vdivsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_divsd, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);

    // vdivss      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_cvtss2sd, EA_4BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
    // vdivsd      xmm0,xmm1,xmm2
    getEmitter()->emitIns_R_R_R(INS_cvtsd2ss, EA_8BYTE, REG_XMM0, REG_XMM1, REG_XMM2);
#endif // FEATURE_AVX_SUPPORT
#endif // ALL_XARCH_EMITTER_UNIT_TESTS
    printf("*************** End of genAmd64EmitterUnitTests()\n");
}

#endif // defined(DEBUG) && defined(LATE_DISASM) && defined(_TARGET_AMD64_)

#endif // _TARGET_AMD64_

#endif // !LEGACY_BACKEND
