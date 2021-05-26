using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// ---------------------------------------------------------------
// This is the prototype for the IL generated stub we build that
// wraps the class being tested.
// ---------------------------------------------------------------
public abstract class TestDynamic
{
    public abstract void Setup(string[] args); // once
    public abstract void Prepare(); // before each run
    public abstract void Run();
    public abstract void Cleanup(); // before each run
};

// ---------------------------------------------------------------
// This class is a simple timer and basic statistics calculator
// ---------------------------------------------------------------
public class PerfTimer
{
    long m_Start;
    long m_End;
    long m_Freq;
    long m_Min;
    long m_Max;
    double m_Count;
    double m_Sum;

    ArrayList m_Samples;

    public PerfTimer() {
        m_Start = m_End = 0;
        m_Min = m_Max = 0;
        m_Count = m_Sum = 0;
        m_Samples = new ArrayList();

        m_Freq = Stopwatch.Frequency;
    }

    public void Start() {
        m_Start = GetMilliseconds();
        m_End = m_Start;
    }
    public void Start(long start) {
        m_End = m_Start = start;
    }

    public void Stop() {
        m_End = GetMilliseconds();
    }
    public void Stop(long end) {
        m_End = end;
    }

    public long GetDuration() { // in milliseconds.
        return (m_End - m_Start);
    }

    public long GetMilliseconds() {
        long i = Stopwatch.GetTimestamp();
        return ((i * 1000L) / m_Freq);
    }

    // These methods allow you to count up multiple iterations and
    // then get the median, Mean and percent variation.
    public void Add(long ms) {
        m_Samples.Add(ms);
        if (m_Min == 0) m_Min = ms;
        if (ms < m_Min) m_Min = ms;
        if (ms > m_Max) m_Max = ms;
        m_Sum += ms;
        m_Count++;
    }

    public long Count { get { return (long)m_Count; } }

    public long Min() {
        return m_Min;
    }

    public long Max() {
        return m_Max;
    }

    public double Median() {
        return TwoDecimals(m_Min + ((m_Max - m_Min)/2.0));
    }

    public double MaxDeviation {
        get
        {
            double maxdev = 0;
            double mean = m_Sum / m_Count;
            for (int i = 0; i < m_Samples.Count; i++)
            {
                long value = (long)m_Samples[i];
                double v = (double)value;
                double d = System.Math.Abs(v - mean);
                if (d > maxdev) maxdev = d;
            }
            return TwoDecimals((maxdev*100.0)/mean);
        }
    }

    public double StdDeviation {
        get
        {
            double mean = m_Sum / m_Count;
            double sumsq = 0;
            for (int i = 0; i < m_Count; i++)
            {
                long value = (long)m_Samples[i];
                double v = (double)value;
                sumsq += System.Math.Pow(v - mean,2);
            }
            return TwoDecimals((System.Math.Sqrt(sumsq/(m_Count-1))*100)/mean);
        }
    }


    public double PercentSpread {
        get
        {
            double spread = (m_Max - m_Min)/2.0;
            double percent = TwoDecimals((spread*100.0)/m_Min);
            return percent;
        }
    }

    public double TwoDecimals(double i)
    {
        return Math.Round(i * 100) / 100;
    }

    public double Mean()
    {
        return TwoDecimals(m_Sum / m_Count);
    }

    public void Clear()
    {
        m_Start = m_End = m_Min = m_Max = 0;
        m_Sum = m_Count = 0;
        m_Samples = new ArrayList();
    }

    public double GetMbs(long len)
    {
        double mbs = ((double)len / 1000000.0) / (Mean()/1000.0);
        return TwoDecimals(mbs);
    }
}

// ---------------------------------------------------------------
// This is class that has the Main entry point and builds the
// IL generated test wrapper, runs the test and reports the results.
// ---------------------------------------------------------------
public class PerfMark {

    public static void Main(string[] args) {
#if LISTEN
        TextWriter output = new StreamWriter(new FileStream("results.out", FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
        System.Console.SetOut(output);  // "redirect" Console.Out

        TraceListener listen = new TextWriterTraceListener(output);
        try {
            Debug.Listeners.Add(listen);
#else
        try {
#endif
            PerfMark.RunTest(args);
        }
        catch(Exception e) {
            Console.WriteLine(e.ToString());
        }
        System.Console.WriteLine();

#if LISTEN
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Debug.Listeners.Remove(listen);
        output.Flush();
#endif
    }
    
    static public void RunTest(string[] args) {
        PerfTimer _timer = new PerfTimer();

        int threads = 1;
        int duration = -1;
        int warmup = -1;
        int mainThreadSleep = 20;
        bool threadpool = true;
        bool threadpoolspin = false;
        string threadstring = "tp";

        ArrayList testargs = new ArrayList();
        string a = null;
        string c = null;
        string m = null;
        bool verbose = false;
        int i;

        for ( i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if ('-' != arg[0]) {
                testargs.Add(arg); // pass through to test.
                continue;
            }
            switch(arg.ToLower()) {
            case "-threadcreate":// how many worker threads to create
            case "-tc":
                threadpool = false;
                threadpoolspin = false;
                threadstring = "tc";
                threads = Int32.Parse(args[++i]);
                break;
            case "-threadpoolqueue":
            case "-threadpool": // how many worker threads to queue
            case "-tp":
            case "-t":
                threadpool = true;
                threadpoolspin = false;
                threadstring = "tp";
                threads = Int32.Parse(args[++i]);
                break;
            case "-threadpoolspin": // how many worker threads to queue
                threadpool = false;
                threadpoolspin = true;
                threadstring = "tps";
                threads = Int32.Parse(args[++i]);
                break;
            case "-duration": // how many seconds will main thread run
            case "-d":
                duration = Int32.Parse(args[++i]);
                break;
            case "-warmup":// how many seconds will main thread warmup on
            case "-w":
                warmup = Int32.Parse(args[++i]);
                break;
            case "-assembly":
            case "-a":   // assembly name
                a = args[++i];
                break;
            case "-class":
            case "-c":   // class name
                c = args[++i];
                break;
            case "-method":
            case "-m":   // method name
                m = args[++i];
                break;
            case "-verbose":
            case "-v":
                verbose = true;
                break;
            case "-continueonexception":
            case "-continue":
                s_ThrowOnExceptions = false;
                break;
            case "-sleep": // how many milliseconds will main thread sleep before checking counter timing
                mainThreadSleep = Math.Min(Int32.Parse(args[++i]), 500);
                break;
            default:
                PrintUsage();
                return;
            }
        }
        if (duration == -1) {
            duration = 10;
        }
        if (a == null)
        {
            Console.WriteLine("*** Error: Must specify assembly to test");
            PrintUsage();
            return;
        }
        if (c == null) {
            c = "Test";
        }

        Assembly ass = Assembly.LoadFrom(a);
        Type test = ass.GetType(c);
        if (test == null) {
            Console.WriteLine("Class '" + c + "' not found in assembly.");
            return;
        }

        ConstructorInfo ci = test.GetConstructor(new Type[0]);
        if (ci == null) {
            Console.WriteLine("Class '" + c + "' does not contain public default constructor.");
            return;
        }

        string[] sargs = new string[testargs.Count];
        for (i = 0; i < testargs.Count; i++) {
            sargs[i] = (string)testargs[i];
        }

        TestDynamic td = GenTest(test, ci, m, sargs);
        if (td == null) {
            return;
        }

        string metric = "ms";

        long startTime = 0, stopTime = 0, lastTime = 0;
        long startRequest = 0, lastRequestTotal = 0;

        s_DataSlot = Thread.AllocateDataSlot();

        // Now invoke the test itself.
        {
            s_PendingThreads = new ManualResetEvent(false);
            s_RequestTotal = 0;

            // prime the pump, and make sure it's all going to work.
            td.Setup(sargs);
            td.Prepare();
            td.Run();
            td.Cleanup();

            _timer.Start();

            if (verbose) {
                Console.Write("Creating tests ");
            }
            s_dynamicTest = new TestDynamic[threads];
            for (i = 0; i < threads; i++) {
                TestDynamic pt = (TestDynamic) Activator.CreateInstance(s_classType);
                pt.Setup(sargs);
                s_dynamicTest[i] = pt;

                if (verbose) {
                    Console.Write(".");
                }
            }
            if (verbose) {
                Console.WriteLine();
            }

            if (verbose) {
                Console.Write("Queueing tests ");
            }
            if (threadpoolspin) {
                for (i = 0; i < threads; i++) {
                    Interlocked.Increment(ref s_running);
                    WaitCallback waitCallback = new WaitCallback(RunThreadPoolSpin);
                    ThreadPool.UnsafeQueueUserWorkItem(waitCallback, s_dynamicTest[i]);

                    if (verbose) {
                        Console.Write(".");
                    }
                }
            }
            else if (threadpool) {
                for (i = 0; i < threads; i++) {
                    Interlocked.Increment(ref s_running);
                    WaitCallback waitCallback = new WaitCallback(RunThreadPool);
                    ThreadPool.UnsafeQueueUserWorkItem(waitCallback, new object[2] { waitCallback, s_dynamicTest[i] } );

                    if (verbose) {
                        Console.Write(".");
                    }
                }
            }
            else { // threadcreate
                Thread[] threadArray = new Thread[threads];
                for (i = 0; i < threads; i++) {
                    threadArray[i] = new Thread(new ThreadStart(RunThreadCreate));
                }
                for (i = 0; i < threads; ++i) {
                    Interlocked.Increment(ref s_running);
                    threadArray[i].Start();

                    if (verbose) {
                        Console.Write(".");
                    }
                }
            }
            if (verbose) {
                Console.WriteLine();
            }

            if (verbose) {
                Console.WriteLine("Warming test:");
            }
            if (warmup != -1) {
                startTime = lastTime = _timer.GetMilliseconds();
                stopTime = lastTime + warmup * 1000;
                startRequest = lastRequestTotal = s_RequestTotal;

                for(;;) {
                    Thread.Sleep(mainThreadSleep);

        		    long currentTime = _timer.GetMilliseconds();
                    long requestTotal = s_RequestTotal;

                    if (1000 <= (currentTime - lastTime)) { // 1 second intervals
                        long total = ((requestTotal - lastRequestTotal) * 1000) / (currentTime - lastTime);

                        lastRequestTotal = requestTotal;
                        lastTime = currentTime;

                        if (verbose) {
                            Console.WriteLine("\tTotal=" + total + " ThreadEncounter=" + s_ThreadsCreated);
                        }
                    }

                    if (s_Continue && (lastTime < stopTime)) {
                        if (/*verbose &&*/ (0 == s_running)) {
                            Console.WriteLine("\t\tThread Starvation");
                        }
                        continue;
                    }
                    break;
                }
            }

            if (verbose) {
                Console.WriteLine("Running test:");
            }
            startTime = lastTime = _timer.GetMilliseconds();
            stopTime = lastTime + duration * 1000;
            startRequest = lastRequestTotal = s_RequestTotal;

            _timer.Start(lastTime);

            for(;;) {
                Thread.Sleep(mainThreadSleep);

        		long currentTime = _timer.GetMilliseconds();
                long requestTotal = s_RequestTotal;

                if (1000 <= (currentTime - lastTime)) { // 1 second intervals
                    long total = ((requestTotal - lastRequestTotal) * 1000) / (currentTime - lastTime);

                    lastRequestTotal = requestTotal;
                    lastTime = currentTime;

                    _timer.Stop(currentTime);
                    _timer.Add(total);
                    if (verbose) {
                        Console.WriteLine("\tTotal=" + total + " ThreadEncounter=" + s_ThreadsCreated);
                    }
                }

                if (s_Continue && (lastTime < stopTime)) {
                    if (/*verbose &&*/ (0 == s_running)) {
                        Console.WriteLine("\t\tThread Starvation");
                    }
                    continue;
                }
                break;
            }

            s_Continue = false;
            s_PendingThreads.WaitOne();

            metric = "req/sec";
        }
        Console.Write(threadstring + " " + c + "." + m);
        Console.Write("\t");
        Console.Write(_timer.Mean());
        if (verbose) Console.Write(" "+metric);
        Console.Write("\t");
        if (verbose) Console.Write("maxdev=");
        Console.Write(_timer.MaxDeviation);
        if (verbose) Console.Write("%");
        Console.Write("\t");
        if (verbose) Console.Write("stddev=");
        Console.Write(_timer.StdDeviation);
        if (verbose) Console.Write("%");
        if (verbose) {
            Console.Write("\t");
            Console.Write((int)((double)(lastRequestTotal - startRequest) / (double)(lastTime - startTime) * 1000));
        }
        Console.WriteLine("");
    }

    static private Type s_classType;

    static private long s_RequestTotal = 0;

    static private TestDynamic[] s_dynamicTest;
    static private int s_TestQueue = 0;
    static private int s_SpinLock = 0;

    static private ManualResetEvent s_PendingThreads;
    static private int s_running = 0;
    static private bool s_Continue = true;

    static private bool s_ThrowOnExceptions = true;

    static private int s_ThreadsCreated;
    static private System.LocalDataStoreSlot s_DataSlot;

    static private TestDynamic GetNextTest() {
        while(0 != Interlocked.CompareExchange(ref s_SpinLock, 1, 0));
        TestDynamic pt = s_dynamicTest[s_TestQueue];
        s_dynamicTest[s_TestQueue] = null;
        s_TestQueue++;
        s_SpinLock = 0;
        return pt;
    }

    static private void RunTestStart() {
        if (null == Thread.GetData(s_DataSlot)) {
            Thread.SetData(s_DataSlot, "");
            Interlocked.Increment(ref s_ThreadsCreated);
        }
    }

    static private void RunTestStop() {
        Interlocked.Decrement(ref s_running);
        if (0 == s_running) {
            s_PendingThreads.Set();
        }
    }

    static private void RuntimeException(Exception e, bool throwOnException) {
        Console.WriteLine(e.ToString());
        if (throwOnException) {
            RunTestStop();

            s_Continue = false;
            throw e;
        }
    }

    static private void RunThread(TestDynamic pt) {
        try {
            pt.Prepare();
            pt.Run();
            pt.Cleanup();

            Interlocked.Increment(ref s_RequestTotal);
        }
        catch (Exception e) {
            RuntimeException(e, s_ThrowOnExceptions);
        }
    }

    static public void RunThreadPool(Object state) { // state == object[2] { (WaitCallback), (TestDynamic) }
        RunTestStart();

        TestDynamic pt = (TestDynamic) ((object[]) state)[1];

        RunThread(pt);

        if (s_Continue) {
            ThreadPool.UnsafeQueueUserWorkItem((WaitCallback) ((object[]) state)[0], state);
        }
        else {
            RunTestStop();
        }
    }

    static public void RunThreadPoolSpin(Object state) { // stae == (TestDynamic)
        RunTestStart();

        TestDynamic pt = (TestDynamic) state;
        try {
            while (s_Continue) {
                RunThread(pt);
            }
        }
        finally {
            RunTestStop();
        }
    }

    static public void RunThreadCreate() {
        RunTestStart();

        TestDynamic pt = GetNextTest();
        try {
            while (s_Continue) {
                RunThread(pt);
            }
        }
        finally {
            RunTestStop();
        }
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: PerfHarness [-t n] [-d n] [-w n] -a n [-c n] [-m n] [-v] [args]");
        Console.WriteLine("This program runs a performance test packaged in a Managed DLL.");
        Console.WriteLine("-tc\tthe number of threads to create");
        Console.WriteLine("-tp\tthe number of work items using ThreadPool");
        Console.WriteLine("-t\tdefault thread model (default 1)");
        Console.WriteLine("-d\tfor single thread tests, the number of iterations (default 10)");
        Console.WriteLine("\tand it will report the Mean time in milliseconds.  For multi-");
        Console.WriteLine("\tthread tests this is the duration of test (in seconds) and it will");
        Console.WriteLine("\treport the Mean number of requests/second.");
        Console.WriteLine("-w\tFor multithreaded tests this specifies a warmup period.");
        Console.WriteLine("-a\tname of assembly to test (required).");
        Console.WriteLine("-c\tname of class in assembly to test (default 'Test').");
        Console.WriteLine("\tThe class implements PerfFramework.PerfTest.");
        Console.WriteLine("-m\tname of method to test (default 'Run').");
        Console.WriteLine("-v\tverbose output (default false)");
        Console.WriteLine("args\tAll other arguments are passed through to the test.");
    }

    public static TestDynamic GenTest(Type test, ConstructorInfo ci, string method, string[] args)
    {
        AppDomain appdom = Thread.GetDomain();

        AssemblyName asmname = new AssemblyName();
        asmname.Name = "CalleAssembly";

        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(asmname, AssemblyBuilderAccess.Run);

        ModuleBuilder mod = assembly.DefineDynamicModule("CalleeModule");

        TypeBuilder tb = mod.DefineType("MyTestDynamic", TypeAttributes.Class | TypeAttributes.Public, typeof(TestDynamic), null);
        MethodInfo mi;

        // define a field for holding instance of test object.
        FieldBuilder testField = tb.DefineField("_test", test, FieldAttributes.Private);

        // and the Setup method.
        Type[] pTypes = { typeof(string[]) };
        MethodBuilder mb = tb.DefineMethod("Setup", MethodAttributes.Public | MethodAttributes.Virtual, Type.GetType("Void"), pTypes);
        ILGenerator il = mb.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // this
        il.Emit(OpCodes.Newobj, ci); // construct the test object
        il.Emit(OpCodes.Stfld, testField); // store test object in private field
        mi = test.GetMethod("Setup");
        if (mi != null) {
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldfld, testField); // load test field from 'this'
            il.Emit(OpCodes.Ldarg_1); // string[] args
            il.Emit(OpCodes.Call, mi);// call the method
        }
        il.Emit(OpCodes.Ret);

        // the Run method (method arg)
        mb = tb.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Virtual, Type.GetType("Void"), null);
        il = mb.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // this
        il.Emit(OpCodes.Ldfld, testField); // load test field from 'this'
        mi = test.GetMethod(method, new Type[0]);
        if (mi == null) {
            Console.WriteLine("Method 'void " + method + "()' missing on class '" + test.Name + "'");
            return null;
        }
        il.Emit(OpCodes.Call, mi);// call the method
        il.Emit(OpCodes.Ret);   // return.

        // the Prepare method.
        mb = tb.DefineMethod("Prepare", MethodAttributes.Public | MethodAttributes.Virtual, Type.GetType("Void"), null);
        il = mb.GetILGenerator();
        mi = test.GetMethod("Prepare");
        if (mi != null) {
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldfld, testField); // load test field from 'this'
            il.Emit(OpCodes.Call, mi);// call the method
        }
        il.Emit(OpCodes.Ret);   // return.

        // the Cleanup method.
        mb = tb.DefineMethod("Cleanup", MethodAttributes.Public | MethodAttributes.Virtual, Type.GetType("Void"), null);
        il = mb.GetILGenerator();
        mi = test.GetMethod("Cleanup");
        if (mi != null) {
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Ldfld, testField); // load test field from 'this'
            il.Emit(OpCodes.Call, mi);// call the method
        }
        il.Emit(OpCodes.Ret);   // return.

        s_classType = tb.CreateType();

        Object instance = Activator.CreateInstance(s_classType);

        TestDynamic td = (TestDynamic)instance;
        return td;
    }
}
