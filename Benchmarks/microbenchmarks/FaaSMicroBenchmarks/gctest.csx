using System.Net;
using System.Threading;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    long stop0 = DateTime.Now.Ticks;

    Allocation.Run();
    long stop1 = DateTime.Now.Ticks;

    EEGC.Run();
    long stop2 = DateTime.Now.Ticks;

    List.Run();
    long stop3 = DateTime.Now.Ticks;

    StringConcat.Run();
    long stop4 = DateTime.Now.Ticks;

    TimeSpan elapsedSpan1 = new TimeSpan(stop1 - stop0);
    TimeSpan elapsedSpan2 = new TimeSpan(stop2 - stop1);
    TimeSpan elapsedSpan3 = new TimeSpan(stop3 - stop2);
    TimeSpan elapsedSpan4 = new TimeSpan(stop4 - stop3);

    TimeSpan elapsedSpan = new TimeSpan(stop4 - stop0);
    return req.CreateResponse(HttpStatusCode.OK, "GC tests runs " + elapsedSpan.TotalMilliseconds + " Allocation: " + elapsedSpan1.TotalMilliseconds + " EEGC: " + elapsedSpan2.TotalMilliseconds + " List: " + elapsedSpan3.TotalMilliseconds + " StringConcat: " + elapsedSpan4.TotalMilliseconds);
}

// Allocation
class Allocation
{
    static public void Run()
    {
        UInt64 MaxBytes = 150000000;
        int byteArraySize = 95000;

        //Allocate memory

        UInt64 objCount = MaxBytes / (UInt64)byteArraySize;

        if (objCount > 0X7FEFFFFF)
        {
            objCount = 0X7FEFFFFF;
        }

        Object[] objList = new Object[objCount];

        int count = (int)objCount;
        for (int j = 0; j < 50; j++)
        {
            for (int i = 0; i < count; i++)
            {
                objList[i] = new byte[byteArraySize];
            }
        }
    }
}


// EECS
public class Node
{
    public Node left;
    public Node right;
}

public class SleepThread
{
    private static int m_sleepTime;
    public static bool shouldContinue;

    public SleepThread(int time)
    {
        m_sleepTime = time;
    }

    public static void ThreadStart()
    {
        run();
    }

    public static void run()
    {
        long tElapsed, t1, t2;
        int cIteration;
        cIteration = 0;

        while (Volatile.Read(ref shouldContinue))
        {
            cIteration++;

            t1 = Environment.TickCount;

            Thread.Sleep(m_sleepTime);

            t2 = Environment.TickCount;

            if (t2 - t1 > m_sleepTime * 1.4)
            {
                tElapsed = t2 - t1;
#if VERBOSE
					Console.WriteLine("Thread 2. Iteration " + cIteration + ". " + tElapsed + "ms elapsed");
#endif
            }
        }
    }
}


public class EEGC
{
    internal static int kStretchTreeDepth;
    internal static int kLongLivedTreeDepth;
    internal static int kShortLivedTreeDepth;
    const int NUM_ITERATIONS = 10000;

    internal static void Populate(int iDepth, Node thisNode)
    {
        if (iDepth <= 0)
            return;

        else
        {
            iDepth--;
            thisNode.left = new Node();
            thisNode.right = new Node();
            Populate(iDepth, thisNode.left);
            Populate(iDepth, thisNode.right);
        }
    }

    public static void Run()
    {
        Node root;
        Node longLivedTree;
        Node tempTree;
        Thread sleepThread;

        kStretchTreeDepth = 19;     // about 24MB
        kLongLivedTreeDepth = 18;   // about 12MB
        kShortLivedTreeDepth = 13;  // about 0.4MB

        tempTree = new Node();
        Populate(kStretchTreeDepth, tempTree);
        tempTree = null;

        longLivedTree = new Node();
        Populate(kLongLivedTreeDepth, longLivedTree);

        SleepThread sThread;
        sThread = new SleepThread(100);
        sleepThread = new Thread(new ThreadStart(SleepThread.ThreadStart));

        sleepThread.Start();

        for (long i = 0; i < NUM_ITERATIONS; i++)
        {
            root = new Node();
            Populate(kShortLivedTreeDepth, root);
        }

        root = longLivedTree;

        SleepThread.shouldContinue = false;
        sleepThread.Join(500);
    }
}

// GCLarge
internal class List
{
    const int LOOP = 847;
    public SmallGC dat;
    public List next;

    public static void Run()
    {
        long iterations = 30;

        //Large Object Collection
        CreateLargeObjects();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        for (long i = 0; i < iterations; i++)
        {
            CreateLargeObjects();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        //Large Object Collection (half array)
        CreateLargeObjectsHalf();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        for (long i = 0; i < iterations; i++)
        {
            CreateLargeObjectsHalf();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        //Promote from Gen1 to Gen2
        SmallGC[] sgc;
        sgc = new SmallGC[LOOP];
        for (int j = 0; j < LOOP; j++)
            sgc[j] = new SmallGC(0);

        GC.Collect();

        for (int j = 0; j < LOOP; j++)
            sgc[j] = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();

        for (long i = 0; i < iterations; i++)
        {
            // allocate into gen 0
            sgc = new SmallGC[LOOP];
            for (int j = 0; j < LOOP; j++)
                sgc[j] = new SmallGC(0);

            // promote to gen 1
            while (GC.GetGeneration(sgc[LOOP - 1]) < 1)
            {
                GC.Collect();
            }

            while (GC.GetGeneration(sgc[LOOP - 1]) < 2)
            {
                // promote to gen 2
                GC.Collect();
            }

            for (int j = 0; j < LOOP; j++)
                sgc[j] = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        //Promote from Gen1 to Gen2 (Gen1 ptr updates)
        List node = PopulateList(LOOP);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (List.ValidateList(node, LOOP) == 0)
            Console.WriteLine("Pointers after promotion are not valid");

        for (long i = 0; i < iterations; i++)
        {
            // allocate into gen 0
            node = PopulateList(LOOP);

            // promote to gen 1
            while (GC.GetGeneration(node) < 1)
            {
                GC.Collect();
            }

            while (GC.GetGeneration(node) < 2)
            {
                //promote to gen 2
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (ValidateList(node, LOOP) == 0)
                    Console.WriteLine("Pointers after promotion are not valid");

            }
        }
    }

    public static List PopulateList(int len)
    {
        if (len == 0) return null;
        List Node = new List();
        Node.dat = new SmallGC(1);
        Node.dat.AttachSmallObjects();
        Node.next = null;
        for (int i = len - 1; i > 0; i--)
        {
            List cur = new List();
            cur.dat = new SmallGC(1);
            cur.dat.AttachSmallObjects();
            cur.next = Node;
            Node = cur;
        }
        return Node;
    }
    public static int ValidateList(List First, int len)
    {
        List tmp1 = First;
        int i = 0;
        LargeGC tmp2;
        while (tmp1 != null)
        {
            //Check the list have correct small object pointers after collection
            if (tmp1.dat == null) break;
            tmp2 = tmp1.dat.m_pLarge;
            //check the large object has non zero small object pointers
            if (tmp2.m_pSmall == null) break;
            //check the large object has correct small object pointers
            if (tmp2.m_pSmall != tmp1.dat) break;
            tmp1 = tmp1.next;
            i++;
        }
        if (i == len)
            return 1;
        else
            return 0;
    }


    public static void CreateLargeObjects()
    {
        LargeGC[] lgc;
        lgc = new LargeGC[LOOP];
        for (int i = 0; i < LOOP; i++)
            lgc[i] = new LargeGC();
    }

    public static void CreateLargeObjectsHalf()
    {
        LargeGC[] lgc;
        lgc = new LargeGC[LOOP];

        for (int i = 0; i < LOOP; i++)
            lgc[i] = new LargeGC();

        for (int i = 0; i < LOOP; i += 2)
            lgc[i] = null;
    }
}

internal class LargeGC
{
    public double[] d;
    public SmallGC m_pSmall;

    public LargeGC()
    {
        d = new double[10625]; //85 KB
        m_pSmall = null;
    }

    public virtual void AttachSmallObjects(SmallGC small)
    {
        m_pSmall = small;
    }
}

internal class SmallGC
{
    public LargeGC m_pLarge;
    public SmallGC(int HasLargeObj)
    {
        if (HasLargeObj == 1)
            m_pLarge = new LargeGC();
        else
            m_pLarge = null;
    }
    public virtual void AttachSmallObjects()
    {
        m_pLarge.AttachSmallObjects(this);
    }
}


// LargeStrings
public class StringConcat
{
    // Objects used by test. init before Main is entered.

    const int NUM_ITERS_CONCAT = 10;
    const int NUM_ITERS = 1000;

    public static String s1 = "11234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s2 = "21234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s3 = "31234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s4 = "41234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s5 = "51234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s6 = "61234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s7 = "71234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s8 = "81234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s9 = "91234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public static String s10 = "01234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static void Run()
    {
        string str = null;

        for (long i = 0; i < NUM_ITERS; i++)
        {
            for (int j = 0; j < NUM_ITERS_CONCAT; j++)
            {
                str += s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10
                    + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8 + s9 + s10;
            }

            str = "";
        }
    }
}