// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Security;
using System;
using System.Runtime.InteropServices; // For SafeHandle


/// <summary>
///IsInvalid
/// </summary>
public class SafeHandleIsInvalid
{
    #region Public Methods

    [SecuritySafeCritical]
    public bool RunTests()
    {
        bool retVal = true;

        TestLibrary.TestFramework.LogInformation("[Positive]");
        retVal = PosTest1() && retVal;
        retVal = PosTest2() && retVal;
        return retVal;
    }

    #region Positive Test Cases

    [SecuritySafeCritical]
    public bool PosTest1()
    {
        bool retVal = true;

        TestLibrary.TestFramework.BeginScenario("PosTest1: Check IsInvalid return true . ");
        try
        {
            MySafeHandle msh = new MySafeHandle();
            IntPtr myIptr = new IntPtr(1000);
            msh.MySetHandle(myIptr);
           
            if (!msh.IsInvalid)
            {
                TestLibrary.TestFramework.LogError("001.1", "IsInvalid should return true");
                retVal = false;
            }

        }
        catch (Exception e)
        {
            TestLibrary.TestFramework.LogError("001.2", "Unexpected exception: " + e);
            TestLibrary.TestFramework.LogInformation(e.StackTrace);
            retVal = false;
        }

        return retVal;
    }


    [SecuritySafeCritical]
    public bool PosTest2()
    {
        bool retVal = true;

        TestLibrary.TestFramework.BeginScenario("PosTest2: Check IsInvalid return false when  the handle value is Released. ");
        try
        {
            MySafeHandle msh = new MySafeHandle();
            IntPtr myIptr = new IntPtr(1000);
            msh.MySetHandle(myIptr);
            msh.MyReleaseInvoke();
            if (msh.IsInvalid)
            {
                TestLibrary.TestFramework.LogError("002.1", "IsInvalid should return false ");
                retVal = false;
            }

        }
        catch (Exception e)
        {
            TestLibrary.TestFramework.LogError("002.2", "Unexpected exception: " + e);
            TestLibrary.TestFramework.LogInformation(e.StackTrace);
            retVal = false;
        }

        return retVal;
    }

    #endregion

    #endregion


    [SecuritySafeCritical]
    public static int Main()
    {
        SafeHandleIsInvalid test = new SafeHandleIsInvalid();

        TestLibrary.TestFramework.BeginTestCase("SafeHandleIsInvalid");

        if (test.RunTests())
        {
            TestLibrary.TestFramework.EndTestCase();
            TestLibrary.TestFramework.LogInformation("PASS");
            return 100;
        }
        else
        {
            TestLibrary.TestFramework.EndTestCase();
            TestLibrary.TestFramework.LogInformation("FAIL");
            return 0;
        }
    }
}

[SecurityCritical]
public class MySafeHandle : SafeHandle
{   
    [SecurityCritical]
    public MySafeHandle()
        : base(IntPtr.Zero, true)
    {
        this.handle = new IntPtr(100);
    }
    bool InvalidValue = true;
    public override bool IsInvalid
    {
        [SecurityCritical]
        get { return InvalidValue; }

    }
    public bool MyReleaseInvoke()
    {
        return ReleaseHandle();
    }
    public void MySetHandle(IntPtr iptr)
    {
        this.SetHandle(iptr);
    }
    public IntPtr GetHandle()
    {
        return this.handle;
    }
    [DllImport("kernel32")]
    private static extern bool CloseHandle(IntPtr handle);

    [SecurityCritical]
    protected override bool ReleaseHandle()
    {
        if (handle == IntPtr.Zero)
        {
            InvalidValue = false;
            return true;
        }
        this.SetHandle(IntPtr.Zero);
        InvalidValue = false;
        return true;
    }
    public bool CheckHandleIsRelease()
    {
        if (handle != IntPtr.Zero)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
   
}