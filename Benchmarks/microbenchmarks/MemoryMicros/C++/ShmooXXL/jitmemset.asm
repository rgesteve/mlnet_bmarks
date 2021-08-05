LEAF_ENTRY macro Name, Section

Section segment para 'CODE'

        align   16

        public  Name
Name    proc

        endm

LEAF_END macro Name, section

Name    endp

Section ends

        endm

LEAF_END_MARKED macro Name, section
        public Name&_End
Name&_End label qword
        ; this nop is important to keep the label in 
        ; the right place in the face of BBT
        nop
        
Name    endp

Section ends

        endm

LEAF_ENTRY JIT_MemSet, _TEXT

        movzx   edx, dl                 ; set fill pattern
        mov     r9, 0101010101010101h   
        imul    rdx, r9                 ; rdx is 8 bytes filler

        cmp     r8, 16                  
        jbe     mset04                 

        cmp     r8, 512                 
        jbe     mset00 
        
        ; count > 512
        mov     r10, rcx                ; save dst address
        mov     r11, rdi                ; save rdi
        mov     eax, edx                ; eax is value
        mov     rdi, rcx                ; rdi is dst
        mov     rcx, r8                 ; rcx is count
        rep     stosb
        mov     rdi, r11                ; restore rdi
        mov     rax, r10
        ret

        align 16
mset00: mov     rax, rcx                ; save dst address
        movd    xmm0, rdx				
        punpcklbw xmm0, xmm0            ; xmm0 is 16 bytes filler

        cmp     r8, 128                
        jbe     mset02  

        ; count > 128 && count <= 512
        mov     r9, r8
        shr     r9, 7                   ; count/128
        
        align 16
mset01: movdqu	[rcx], xmm0
        movdqu	16[rcx], xmm0
        movdqu	32[rcx], xmm0
        movdqu	48[rcx], xmm0
        movdqu	64[rcx], xmm0
        movdqu	80[rcx], xmm0
        movdqu	96[rcx], xmm0
        movdqu	112[rcx], xmm0
        add     rcx, 128
        dec     r9
        jnz     mset01    
        and     r8, 7fh                 ; and r8 with 0111 1111
        
        ; the remainder is from 0 to 127
        cmp     r8, 16                  
        jnbe    mset02                  
        
        ; the remainder <= 16 
        movdqu  -16[rcx + r8], xmm0
        ret
        
        ; count > 16 && count <= 128 for mset02
        align 16
mset02: movdqu	[rcx], xmm0         
        movdqu	-16[rcx + r8], xmm0    
        cmp     r8, 32                 
        jbe     mset03
        
        ; count > 32 && count <= 64
        movdqu	16[rcx], xmm0
        movdqu	-32[rcx + r8], xmm0
        cmp     r8, 64
        jbe     mset03
        
        ; count > 64 && count <= 128
        movdqu	32[rcx], xmm0
        movdqu	48[rcx], xmm0
        movdqu	-48[rcx + r8], xmm0
        movdqu	-64[rcx + r8], xmm0   
mset03: ret
 
        align 16
mset04: mov     rax, rcx                ; save dst address
        test    r8b, 24                 ; and r8b with 0001 1000
        jz      mset05
        
        ; count >= 8 && count <= 16
        mov     [rcx], rdx        
        mov     -8[rcx + r8], rdx
        ret

        align 16
mset05: test    r8b, 4                  ; and r8b with 0100
        jz      mset06
        
        ; count >= 4 && count < 8
        mov     [rcx], edx        
        mov     -4[rcx + r8], edx
        ret
        
        ; count >= 0 && count < 4
        align 16
mset06: test    r8b, 1                  ; and r8b with 0001
        jz      mset07
        mov     [rcx],dl
mset07: test    r8b, 2                  ; and r8b with 0010
        jz      mset08
        mov     -2[rcx + r8], dx
mset08: ret

LEAF_END_MARKED JIT_MemSet, _TEXT

;JIT_MemCpy - Copy source buffer to destination buffer
;
;Purpose:
;   JIT_MemCpy() copies a source memory buffer to a destination memory
;   buffer. This routine recognize overlapping buffers to avoid propogation.
;   For cases where propogation is not a problem, memcpy() can be used.
;
;Algorithm:
;Copy to destination based on count as follow
;   count [0, 64]: overlap check not needed
;       count [0, 16]: use 1/2/4/8 bytes width registers  
;       count [16, 64]: use 16 bytes width registers (XMM) without loop
;   count [64, upper]: check overlap
;       non-overlap:
;           count [64, 512]: use 16 bytes width registers (XMM) with loops, unrolled 4 times
;           count [512, upper]: use rep movsb
;       overlap::
;           use 16 bytes width registers (XMM) with loops to copy from end to beginnig
;
;Entry:
;   void *dst = pointer to destination buffer
;   const void *src = pointer to source buffer
;   size_t count = number of bytes to copy
;
;Exit:
;   Returns a pointer to the destination buffer
;
;Uses:
;
;Exceptions:
;*******************************************************************************

LEAF_ENTRY JIT_MemCpy, _TEXT
        ;mov     r9d, [rdx]
        ;mov     r10d, -4[rdx + r8]
        ;mov     [rcx], r9d
        ;mov     -4[rcx + r8], r10d
        ret
LEAF_END_MARKED JIT_MemCpy, _TEXT
		end
        end