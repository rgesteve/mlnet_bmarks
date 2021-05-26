using System;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Specjbb2005.src.spec.jbb.Validity;

namespace Specjbb2005.src.spec.jbb
{
    /// <summary>
    /// Summary description for JBBMain.
    /// </summary>
    public class JBBmain
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 

        //  internal static readonly String COPYRIGHT = "SPECjbb2005,"
        //      + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
        //      + "All rights reserved,"
        //      + "(C) Copyright IBM Corp., 1996 - 2005"
        //      + "All rights reserved,"
        //      + "Licensed Materials - Property of SPEC";

        public static readonly String Version = "SPECjbb2005 1.04";
        public static readonly String VersionDate = "June 13, 2005";

        public static readonly String[] Header =
        {
            "",
            "Licensed Materials - Property of SPEC",
            "SPECjbb2005",
            "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
            + "All rights reserved,"
        };

        public static readonly String[] TPC_FAIR_USE =
        {
            "",
            "Licensed Materials - Property of SPEC",
            "SPECjbb2005",
            "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
            + "All rights reserved,"
            + "(C) Copyright IBM Corp., 1996 - 2005"
            + "All rights reserved," + "",
            "This source code is provided as is, without any express or implied warranty.",
            "",
            "TPC Fair Use policy:",
            "",
            "SPECjbb2005 is not a TPC Benchmark. SPECjbb2005 results are not comparable with",
            "any TPC Benchmark results. The workload used by SPECjbb2005 is inspired",
            "by the TPC-C specification, TPC Benchmark C , Standard Specification,",
            "Revision 3.2, August 27 1996. TPC Benchmark is a trademark of the Transaction",
            "Processing Performance Council."
        };

        private static long deterministic_seed = 2108417082252868L;

        public static readonly short NON_NUMERIC_ENTRY = -99;

        public static Company myCompany = null;

        //TODO:convert these to <> since it is available only in whidbey.
        //public Vector<Thread>        threadList;
        //public Vector<Short>         whIdStack;

        public List<Task> threadList = new List<Task>();
        public ArrayList whIdStack;

        //public List<Thread>                threadList;
        //public List<short>                whIdStack;


        private int testnum = 0;

        private int warehousePopulationBase;

        private int orderlinesPerOrder;

        private int waitTimePercent;

        private bool forceGC;

        private bool screenWriteFlag;

        private bool steadyStateFlag;

        public static bool uniformRandomItems;

        public static int overrideItemTableSize = 0;

        public static int maxWh = 2;

        static private StreamWriter outResultsFile = null;

        static private StreamWriter outRawFile = null;

        static private StreamWriter outDeliveriesFile = null;

        // multi-JVM variables
        static public bool multiJVMMode = false;

        static public int instanceId = -1;

        static public int port = 1500;

        static public StreamReader socIn;

        //static public PrintWriter    socOut; //for multijvm. don;t need it for CLR.

        static public String defaultOutputDir = "results";

        static public String defaultPropsFileName = "SPECjbb.props";

        JBBProperties prop;
        /*
        public void run()
        {
            TransactionManager transMgr = null;
            // Vector.remove(n) method returns n-element and deletes it
            //short wId = ((Short) whIdStack.remove(0)).shortValue();
            short wId;
            //09-11-07 Li: Need have this lock so that multiple threads access it
            //will not step on each other, which potentially can cause deadlock.
            lock (whIdStack)
            {
                wId = (short)whIdStack[0]; whIdStack.RemoveAt(0);//get it and then remove it.
            }
            int maxwh = myCompany.getMaxWarehouses();
            if ((wId > 0) && (wId <= myCompany.getMaxWarehouses()))
            {
                transMgr = new TransactionManager();
                transMgr.initTransactionManager(myCompany, wId);
                // add transMgr instance to ArrayList in Company
                myCompany.addWarehouseThread(transMgr);
                transMgr.go();
            }
            else
            {
                //JBButil.getLog().warning(
                Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                    "IMPOSSIBLE ERROR: Invalid Warehouse passed in.  Value was "
                    + wId + " and should be between 1 and "
                    + myCompany.getMaxWarehouses());
                JBButil.SecondsToSleep(15);
            }
        }//run
        */
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        /*
        public void startJBBthread(short whID)
        {
            lock (_syncRoot)
            {
                if (whIdStack == null)
                {
                    //whIdStack = new List<short>();//new Vector<Short>();
                    whIdStack = ArrayList.Synchronized(new ArrayList());
                }

                // CORECLR Use Task instead of Threads

                //Thread whThread = new Thread(new ThreadStart(this.run));
                //threadList.Add(whThread);
                // whIdStack.Add(whID);
                //whThread.Start();

                whIdStack.Add(whID);


                Task[] t = new Task[1];
                Task tsk = Task.Run(() =>
                {
                    try
                    {
                        // CORECLR run function
                        //
                        TransactionManager transMgr = null;
                        // Vector.remove(n) method returns n-element and deletes it
                        //short wId = ((Short) whIdStack.remove(0)).shortValue();
                        short wId = 0;
                        //09-11-07 Li: Need have this lock so that multiple threads access it
                        //will not step on each other, which potentially can cause deadlock.
                        lock (whIdStack)
                        {
                            Console.WriteLine($"Debug: whIdStack.Count = {whIdStack.Count}");
                            if (whIdStack.Count > 0)
                            {
                                wId = (short)whIdStack[0];
                                whIdStack.RemoveAt(0);//get it and then remove it.
                            }
                        }
                        int maxwh = myCompany.getMaxWarehouses();
                        if ((wId > 0) && (wId <= myCompany.getMaxWarehouses()))
                        {
                            transMgr = new TransactionManager();
                            transMgr.initTransactionManager(myCompany, wId);
                            // add transMgr instance to ArrayList in Company
                            myCompany.addWarehouseThread(transMgr);
                            transMgr.go();
                        }
                        else
                        {
                            Console.WriteLine($"ProcessorCount: {Environment.ProcessorCount}");

                            //JBButil.getLog().warning(
                            Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                                    "IMPOSSIBLE ERROR: Invalid Warehouse passed in.  Value was "
                                    + wId + " and should be between 1 and "
                                    + myCompany.getMaxWarehouses());
                            JBButil.SecondsToSleep(15);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProcessorCount: {Environment.ProcessorCount}");
                        Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
                    }

                });
                threadList.Add(tsk);
                whIdStack.Add(whID);
            }
        }//startJBBthread


        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void stopJBBthread()
        {
            lock (_syncRoot)
            {
                //while (!threadList.isEmpty()) 
                while (threadList.Count != 0)
                {
                    try
                    {
                        while ((threadList[0].Status == TaskStatus.Running))
                        {
                            JBButil.SecondsToSleep(1);
                        }
                        threadList.RemoveAt(0); //Once you remove 0th element, 1st element
                                                //becomes 0th and so no need to have a loop.
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Console.WriteLine("No first element in the thread list: " + e.Message);
                        Console.WriteLine(e.StackTrace);
                    }
                }
            }//stopJBBthread
        }
        public void DoARun(Company myCompany, short number_of_warehouses,
            int rampup_time, int measurement_time)
        {
            //JBButil.getLog().entering("spec.jbb.JBBmain", "DoARun");
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.JBBmain::DoARun");
            short whID;
            testnum++;
            myCompany.setPropOutputPrefix("result.test" + testnum + ".");
            Console.WriteLine("Start User Threads");
            Trace.WriteLineIf(JBButil.getLog().TraceInfo, "Start User Threads");//JBButil.getLog().info("Start User Threads");
            myCompany.prepareForStart();
            try
            {
                for (whID = 1; whID <= number_of_warehouses; whID++)
                {
                    myCompany.startAutomated(whID, rampup_time, measurement_time);
                    startJBBthread(whID);

                    String msg = "  started user thread for Warehouse " + whID;
                    Console.WriteLine(msg);
                    Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);

                    // JBButil.SecondsToSleep(1);
                }
            }
             // Don't have equivalent thing here. Just catch OOM.
			//catch (Exception e) catch (java.lang.ThreadDeath e) {
			//{
			//	try 
			//	{
			//		// Be careful that we do not run out of memory trying to log a
			//		// problem that may have been caused by running out of memory.
			//		//JBButil.getLog().log(
			//		Trace.WriteLineIf(JBButil.getLog().TraceWarning,
			//			"ERROR:  A thread died, probably out of memory."
			//			+ "  Increase the heap size and run again");
			//	}
			//	catch (OutOfMemoryException oome) 
			//	{
			//		// Ok, the logging did not work, so just print a message and
			//		// stack trace.
			//		Console.WriteLine("ERROR:  A thread died, probably out of memory."
			//			+ "  Increase the heap size and run again");
			//		Console.WriteLine(e.StackTrace) ;
			//	}
			//}
            catch (OutOfMemoryException e)
            {
                try
                {
                    // Be careful that we do not run out of memory trying to log a
                    // problem caused by running out of memory.
                    //JBButil.getLog().log(
                    //Level.WARNING,
                    Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                        "ERROR:  Out of memory error caught! "
                        + "  Increase the heap size and run again. " + e.Message);
                }
                catch (OutOfMemoryException oome)
                {
                    // Ok, the logging did not work, so just print a message and
                    // stack trace.
                    Console.WriteLine("ERROR:  Out of memory error caught! "
                        + "  Increase the heap size and run again. " + oome.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR:  A thread died, probably out of memory." +
                           "  Increase the heap size and run again  " + e.Message);
                Console.WriteLine(e);
                //e.printStackTrace();
                Console.WriteLine(e.StackTrace);
            }

            myCompany.displayResultTotals(prop.showWarehouseDetail);

            stopJBBthread();
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.JBBmain::DoARun");
            // Push all of the logged messages out after each run
            flushLog();
        }//stopJBBthread()
        */
        public void JbbRun(Company myCompany, short number_of_warehouses, int rampup_time, int measurement_time)
        {
            Console.WriteLine("*** JBBRun ****");

            Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.JBBmain::DoARun");
            testnum++;
            myCompany.setPropOutputPrefix("result.test" + testnum + ".");
            Console.WriteLine("Start User Threads");
            Trace.WriteLineIf(JBButil.getLog().TraceInfo, "Start User Threads");//JBButil.getLog().info("Start User Threads");
            myCompany.prepareForStart();

            Task[] tasks = new Task[number_of_warehouses];
            short wareHouseId = 1;
            for (int i = 0; i < number_of_warehouses; i++)
            {
                int tempIndex = i;
                tasks[tempIndex] = Task.Factory.StartNew(() =>
                {
                    short wId = wareHouseId++;

                    // Console.WriteLine($"Task-Started: ThreadId: {Thread.CurrentThread.ManagedThreadId}  WareHouseId: {wId}");

                    myCompany.startAutomated(wId, rampup_time, measurement_time);
                    try
                    {
                        // CORECLR run function
                        //
                        TransactionManager transMgr = null;

                        transMgr = new TransactionManager();
                        transMgr.initTransactionManager(myCompany, wId);
                        // add transMgr instance to ArrayList in Company
                        myCompany.addWarehouseThread(transMgr);
                        transMgr.go();
                        // Console.WriteLine($"Task-Ended: ThreadId: {Thread.CurrentThread.ManagedThreadId}  WareHouseId: {wId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ProcessorCount: {Environment.ProcessorCount}");
                        Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
                    }
                });
            }

            myCompany.displayResultTotals(prop.showWarehouseDetail);
            Task.WaitAll(tasks);


            // stopJBBthread();
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "exiting spec.jbb.JBBmain::DoARun");
            // Push all of the logged messages out after each run
            flushLog();
        }

        public bool DoAValidationRun(Company myCompany)
        {
            myCompany.prepareForStart();
            myCompany.startValidation((short)1); // one warehouse
            TransactionManager transMgr = new TransactionManager();
            transMgr.initTransactionManager(myCompany, (short)1); // one warehouse
            return transMgr.goValidate();
        }//DoAValidationRun

        public void doIt()
        {
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                            "entering spec.jbb.JBBmain::DOIT");
            // min_btps = min_tpmc *
            // (2.3 = all_transactions / new_order_transactions) /
            // (60 = seconds / minute)
            float min_btps = (float)((prop.minBTPSRatio * 100.0 / prop.waitTimePercent) * 2.3 / 60.0);
            Transaction.setOrderLineCount(prop.orderlinesPerOrder);
            myCompany = new Company();
            // handle deterministic_random_seed
            if (prop.deterministicRandomSeed == true)
            {
                JBButil.set_random_seed(deterministic_seed);
            }
            myCompany.setJVMInstanceValues(prop.jvm_instances);
            myCompany.setMultiJVMRampingValues(prop.per_jvm_warehouse_rampup, prop.per_jvm_warehouse_rampdown);
            myCompany.setPopulationValues(prop.warehousePopulationBase,
                JBBProperties.overrideItemTableSize);
            Transaction.setLogWrite(prop.screenWriteFlag);
            Transaction.setSteadyState(prop.steadyStateFlag);
            int cur_warehouses = 0;
            int num_wh;
            if (prop.sequenceOfWarehouses == null)
            {
                for (num_wh = prop.startingNumberWarehouses; num_wh <= prop.endingNumberWarehouses; num_wh += prop.incrementNumberWarehouses)
                {
                    if (!runWarehouse(cur_warehouses, num_wh, min_btps))
                        break;
                    cur_warehouses = num_wh;
                }
            }
            else
            {
                for (int seqndx = 0; seqndx < prop.sequenceOfWarehouses.Length; seqndx++)
                {
                    num_wh = prop.sequenceOfWarehouses[seqndx];
                    if (!runWarehouse(cur_warehouses, num_wh, min_btps))
                        break;
                    cur_warehouses = num_wh;
                }
            }
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                                    "exiting spec.jbb.JBBmain::DOIT");
        }//doIt

        public bool doItForValidation()
        {
            Transaction.setOrderLineCount(orderlinesPerOrder);
            myCompany = new Company();
            long validationSeed = 528562479389981L;
            JBButil.set_random_seed(validationSeed);
            myCompany.setPopulationValues(warehousePopulationBase);
            Transaction.setLogWrite(screenWriteFlag);
            Transaction.setSteadyState(steadyStateFlag);
            Transaction.setValidation(/*true*/false);//setting it to false: Ask Milind why is that.
            increaseNumWarehouses(0, 1, waitTimePercent);
            bool runValid = DoAValidationRun(myCompany);
            runValid = true;//asking Milind why...
                            //Transaction.setValidation(false); Don;t need this since it is already false.
            return runValid;
        }//doItForValidation

        private void increaseNumWarehouses(int current, int next,
            int waitTimePercent)
        {
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        "entering spec.jbb.JBBmain::increaseNumWarehouses");
            for (int i = current + 1; i <= next; i++)
            {
                myCompany.primeWithDummyData((short)i, 0);
                myCompany.getTimerDataPtr((short)i).useWaitTimesPercentage(
                    waitTimePercent);
            }
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.JBBmain::increaseNumWarehouses");
        }//increaseNumWarehouses

        public bool runWarehouse(int cur_warehouses, int num_wh, float min_btps)
        {
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        "entering spec.jbb.JBBmain::runWarehouse");
            Console.WriteLine("\n++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++\n");
            increaseNumWarehouses(cur_warehouses, num_wh, prop.waitTimePercent);

            if (num_wh < prop.expectedPeakWarehouse)
            {
                JbbRun(myCompany, (short)myCompany.getMaxWarehouses(), 0,
                    prop.rampupSeconds);
            }
            else
            {
                JbbRun(myCompany, (short)myCompany.getMaxWarehouses(), 0,
                    prop.measurementSeconds);
            }
            if (prop.checkThroughput)
            {
                TimerData companyTimerDataPtr = myCompany
                    .getTimerDataPtr((short)0);
                double result = companyTimerDataPtr.getBTPS();
                if (result < (min_btps * num_wh))
                {
                    Console.WriteLine("result below min for warehouse");
                    Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        "exiting spec.jbb.JBBmain::runWarehouse");
                    return false;
                }
            }
            Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.JBBmain::runWarehouse");
            return true;
        }//runWarehouse

        public String commandLineParser(String[] args)
        {
            String s = null;
            if (args.Length == 0)
            {
                s = defaultPropsFileName;
            }
            else if (args[0].Equals("-id"))
            {
                JBBmain.instanceId = int.Parse(args[1]);//Integer.parseInt(args[1]);
            }
            else if (args[0].Equals("-propfile"))
            {
                if (args.Length == 2)
                {
                    s = args[1];
                }
                else
                {
                    if (args.Length == 1)
                    {
                        Console.WriteLine("Missing properties file name");
                        Console.WriteLine("   Parameters:  -propfile <properties_file_name> [-id <instance_id>]");
                    }
                    else if (args.Length == 4 && args[2].Equals("-id"))
                    {
                        s = args[1];
                        JBBmain.instanceId = int.Parse(args[3]);//Integer.parseInt(args[3]);
                    }
                    else
                    {
                        Console.WriteLine("Too many parameters");
                        Console.WriteLine("   Parameters:  -propfile <properties_file_name> [-id <instance_id>]");
                    }
                }
            }
            else
            {
                Console.WriteLine("Unrecognized command line parameter:  "
                    + args[0]);
                Console.WriteLine("   Parameters:  -propfile <properties_file_name>");
            }
            return s;
        }//commandLineParser

        public bool initOutputDir(String outputDir)
        {
            if (outputDir == null)
            {
                // output_directory not specified -- take default
                outputDir = defaultOutputDir;
            }
            DirectoryInfo output_directory_file = new DirectoryInfo(outputDir);
            if (output_directory_file.Exists)
            {
                // File exists -- is it a directory?
                /*if (!output_directory_file.isDirectory()) 
				{
					Console.WriteLine("ERROR:  Specified input.output_directory is not a directory:  "
						+ outputDir);
					return false;
				}*/
            }
            else
            { // Specified directory does not exist -- try to create
              /*if (!output_directory_file.Create()) 
              {
                  Console.WriteLine("ERROR:  Cannot create input.output_directory:  "
                      + outputDir);
                  return false;
              }*/
                output_directory_file.Create(); //this is a void
            }
            return true;
        }//initOutputDir

        public void callReporter(String output_directory, String outRawFile_name,
            String outRawPrefix, String sequenceNumber)
        {
            // bool opth = false;
            // amt: call Reporter
            String msg = "Calling Reporter";
            Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(msg);
            String optr = outRawFile_name;
            String optl = sequenceNumber; // seq #
            String opto = output_directory + Path.DirectorySeparatorChar + outRawPrefix
                + sequenceNumber + ".html";
            String file_Ascii = output_directory + Path.DirectorySeparatorChar + outRawPrefix
                + sequenceNumber + ".txt";


            try
            {
                //TextiReport ar = new spec.reporter.TextiReport(optn, optr, opts);
                //ar.print(file_Ascii);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occurred while generating ASCII reporter. Message = {0}", e.Message); //milind
            }

            try
            {
                //r = new spec.reporter.Report(opte, opts, optn, optr, optv, optc,
                //    optl, opth, output_directory);
            }
            catch (Exception e)
            {
                // opth = true;
                Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                    "Producing html chart in report instead of JPEG; see Users' Guide " + e.Message);
                //r = new spec.reporter.Report(opte, opts, optn, optr, optv, optc,
                //    optl, opth, output_directory);
            }/*
			catch (Exception  e) 
			{
				opth = true;
				JBButil
					.getLog()
					.warning(
					"Producing html chart in report instead of JPEG; see Users' Guide");
				r = new spec.reporter.Report(opte, opts, optn, optr, optv, optc,
					optl, opth, output_directory);
			}
			catch (Exception e) 
			{
				opth = true;
				JBButil
					.getLog()
					.warning(
					"Producing html chart in report instead of JPEG; see Users' Guide");
				r = new spec.reporter.Report(opte, opts, optn, optr, optv, optc,
					optl, opth, output_directory);
			}
			catch (java.lang.Error e) 
			{
				opth = true;
				JBButil
					.getLog()
					.warning(
					"Producing html chart in report instead of JPEG; see Users' Guide");
				r = new spec.reporter.Report(opte, opts, optn, optr, optv, optc,
					optl, opth, output_directory);
			}*/
             //r.print(opto);

            try
            {
                StreamReader AscBr = new StreamReader(new FileStream(file_Ascii, FileMode.Open, FileAccess.Read));
                String s;
                while ((s = AscBr.ReadLine()) != null)
                {
                    Console.WriteLine(s);
                }
            }
            catch (IOException e)
            {

                Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                    "Error opening ASCII output file " + e.Message);
            }
            Console.WriteLine("Output files: " + file_Ascii + ", " + outRawFile_name + ", " + opto);
            Console.WriteLine();
            Console.WriteLine();
            Console.Out.Flush();
            //Console.WriteLine("Reporter messages:");
            //Console.WriteLine(r.messages());
        }//callReporter

        [STAThread]
        public void JBBmainMain(string[] args)
        {
            JBBmain main;
            String outRawPrefix = "SPECjbb.";
            String outRawSuffix = ".raw";
            // CORECLR commented the next line.
            // bool passed_200_check = !(Check.doCheck());
            bool passed_200_check = false;
            main = new JBBmain();
            main.warehousePopulationBase = 30;
            main.orderlinesPerOrder = 10;
            main.waitTimePercent = 0;
            main.forceGC = false;
            main.screenWriteFlag = false;
            main.steadyStateFlag = true;
            JBBmain.uniformRandomItems = true;
            // adding java.awt.headless=true to the system properties
            //String oldprop = System.setProperty("java.awt.headless", "true"); //Do we really need this crap?
            // System.out.println(java.awt.GraphicsEnvironment.isHeadless());
            StreamWriter scratch1 = null;
            StreamWriter scratch2 = null;
            // The DOIT_validation method is going to do some transactions before we
            // are
            // ready to initialize the application logger. So for now we will use
            // the
            // default global logger until we are ready to create the real one.
            //JBButil.setLog(Logger.getLogger("global"));
            //JBButil.getLog().setLevel(Level.WARNING);
            // Enclose the entire "main" execution of the application in a try
            // region
            // so that if there is an unhandled exception we can flush and close the
            // logging file stream before ending the application.

            //This is a temporary log file for Validation. Once the validation is complete
            //We will just delete this file and create a new listener for actual run.
            //
            // CORECLR Environement.GetCurrentDirectory to Directory.GetCurrentDirectory
            String logFile_validation_name = Directory.GetCurrentDirectory() +
                        Path.DirectorySeparatorChar + "ValidationLog.txt";
            if (!main.initApplicationLogging(logFile_validation_name, true))
            {
                Console.WriteLine("ERROR:  Logging initialization failed!");
                return;
            }
            try
            {
                // CORECLR refactor

                using (scratch1 = new StreamWriter(new FileStream("JBB.temp.scratch1", FileMode.Create, FileAccess.Write)))
                using (scratch2 = new StreamWriter(new FileStream("JBB.temp.scratch2", FileMode.Create, FileAccess.Write)))
                    Company.setOutputs(scratch1, scratch2);
                /*
                    try
                    {
                        scratch1 = new StreamWriter(new FileStream("JBB.temp.scratch1", FileMode.Create, FileAccess.Write));
                        scratch2 = new StreamWriter(new FileStream("JBB.temp.scratch2", FileMode.Create, FileAccess.Write));
                    }
                    catch (IOException e)
                    {
                        Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                            "VALIDATION ERROR:  IOException: " + e.Message);
                    }
				Company.setOutputs(scratch1, scratch2);
                */

                bool passed_validation = main.doItForValidation();
                main = null;
                // CORECLR scratch1.Close();
                // CORECLR scratch2.Close();

                FileInfo tfile = new FileInfo("JBB.temp.scratch1");
                tfile.Delete();
                tfile = new FileInfo("JBB.temp.scratch2");
                tfile.Delete();

                Trace.Listeners.Clear();
                Trace.Close(); //Release the file.
                               //File.Delete(logFile_name); //Added by Milind

                main = new JBBmain();
                main.prop = new JBBProperties(main.commandLineParser(args));
                if (!main.prop.getProps())
                {
                    Console.WriteLine("ERROR:  Properties File error; please start again");
                    return;
                }
                // CORECLR Assignment made to the same variable
                // overrideItemTableSize = JBBmain.overrideItemTableSize;

                // elian: Single and Multi JVM run
                // String output_directory = main.prop
                // .getOptionalProperty("input.output_directory");
                // if (!main.initOutputDir(output_directory)) {
                // return;
                // }
                String output_directory = null;
                if (main.prop.jvm_instances > 1)
                {
                    JBBmain.multiJVMMode = true;
                }
                if (!multiJVMMode)
                {
                    output_directory = main.prop
                        .getOptionalProperty("input.output_directory")
                        + Path.DirectorySeparatorChar + "SPECjbbSingleJVM";
                    if (!main.initOutputDir(output_directory))
                    {
                        return;
                    }
                }
                else
                {   /*
					String parentDir = main.prop
						.getOptionalProperty("input.output_directory");
					RunSequencer currentDirRS = new RunSequencer(parentDir,
						"SPECjbbMultiJVM.", "");
					output_directory = parentDir + File.separator
						+ "SPECjbbMultiJVM."
						+ (currentDirRS.padNumber(currentDirRS.getSeq() - 1));
					*/ //multi jvm don;t need for CLR.
                }
                String sequenceNumber;
                if (main.prop.jvm_instances > 1)
                {
                    String n = sequenceNumber = instanceId.ToString();
                    sequenceNumber = "" + n;
                    int returnStringLength = sequenceNumber.Length;
                    if (returnStringLength == 1)
                    {
                        sequenceNumber = "00" + sequenceNumber;
                    }
                    if (returnStringLength == 2)
                    {
                        sequenceNumber = "0" + sequenceNumber;
                    }
                }
                else
                {
                    RunSequencer rs = new RunSequencer(output_directory,
                        outRawPrefix, outRawSuffix);
                    sequenceNumber = rs.getSeqString();
                }
                String outResultsFile_name = output_directory + Path.DirectorySeparatorChar
                    + "SPECjbb." + sequenceNumber + ".results";
                String outRawFile_name = output_directory + Path.DirectorySeparatorChar
                    + outRawPrefix + sequenceNumber + outRawSuffix;
                String outDeliveriesFile_name = output_directory + Path.DirectorySeparatorChar
                    + "SPECjbb." + sequenceNumber + ".deliveries";
                String logFile_name = output_directory + Path.DirectorySeparatorChar
                    + "SPECjbb." + sequenceNumber + ".log";
                Console.WriteLine("The results will be in: " + outRawFile_name);


                SaveOutput.start(outResultsFile_name);
                Console.WriteLine("Opened " + outResultsFile_name);

                using (outRawFile = new StreamWriter(new FileStream(outRawFile_name, FileMode.Create)))
                {
                    Console.WriteLine("Opened " + outRawFile_name);
                    using (outDeliveriesFile = new StreamWriter(new FileStream(outDeliveriesFile_name, FileMode.Create)))
                    {
                        Console.WriteLine("Opened " + outDeliveriesFile_name);
                        Company.setOutputs(outRawFile, outDeliveriesFile);
                        // WRR: Converted to array to avoid use of \n.

                        foreach (String str in Header)
                        {
                            Console.WriteLine(str);
                        }
                        //for (String str : Header) 
                        //{
                        //	Console.WriteLine(str);
                        //}
                        Console.WriteLine("");
                        Console.WriteLine("Benchmark " + Version + " now Opening");
                        Console.WriteLine("");
                        // CORECLR Console.Out.Flush(); //fffffffffffffffffffffffffffffffff
                        // Define maximum number of warehouses
                        if (main.prop.sequenceOfWarehouses == null)
                        {
                            maxWh = main.prop.endingNumberWarehouses + 1;
                        }
                        else
                        {
                            maxWh = main.prop.sequenceOfWarehouses[main.prop.sequenceOfWarehouses.Length - 1] + 1;
                        }
                        // Instantiate and initialize logging utilities for this application

                        //we will be calling this routine twise here. One for validation
                        //and this one for actual application. This is becuase java has a notion
                        //of global logger and they close it and start this app logger here. 

                        if (!main.initApplicationLogging(logFile_name, false))
                        {
                            Console.WriteLine("ERROR:  Logging initialization failed!");
                            return;
                        }
                        // Logging has now been initialized. Log this event.
                        Trace.WriteLineIf(JBButil.getLog().TraceInfo, "Logging started");
                        main.prop.setProp("config.benchmark_version", Version);
                        main.prop.setProp("config.benchmark_versionDate", VersionDate);
                        //main.prop.setProp("config.test.date", DateFormat.getDateInstance()
                        //	.format(new Date()));
                        main.prop.setProp("config.test.date", DateTime.Now.ToString());
                        if (!main.prop.copyPropsToOutput(outRawFile))
                        {
                            Console.WriteLine("ERROR:  Properties File error; please start again");
                            return;
                        }
                        String value1 = main.prop.setProp("result.validity.200_check", passed_200_check.ToString());
                        //new Boolean(passed_200_check).ToString());
                        outRawFile.WriteLine("result.validity.200_check" + "=" + value1);
                        value1 = main.prop.setProp("result.validity.jbb_operation", passed_validation.ToString());
                        //new Boolean(passed_validation).ToString());
                        outRawFile.WriteLine("result.validity.jbb_operation" + "=" + value1);
                        //digest d = new digest(); don;t need it I think.
                        //boolean _999_checkit = d.crunch_jar("jbb.jar");
                        //value1 = main.prop.setProp("result.validity.999_checkit",_999_checkit.ToString);Don;t need it. 
                        //new Boolean(_999_checkit).ToString());
                        outRawFile.WriteLine("result.validity.999_checkit" + "=" + value1);
                        main.prop.checkCompliance();
                        // Before starting the main benchmark flush the logs
                        main.flushLog();
                        // set up socket communication for multi-jvm mode. Don;t need it for C#
                        /*
                        if (JBBmain.multiJVMMode) 
                        {
                            String msg = "Running Multi-JVM Test: socket "
                                + (port + JBBmain.instanceId);
                            JBButil.getLog().info(msg);
                            Console.WriteLine(msg);
                            Socket soc = null;
                            boolean trySucceeded = false;
                            int tries = 0;
                            while (!trySucceeded && (tries < 10)) 
                            {
                                try 
                                {
                                    tries++;
                                    if (tries > 1) 
                                    {
                                        Thread.sleep(5000);
                                    }
                                    soc = new Socket("localhost", port + JBBmain.instanceId);
                                    trySucceeded = true;
                                }
                                catch (Exception e) 
                                {
                                    //
                                    // * JBButil.getLog().log(Level.WARNING, e + ": error in
                                    // * creating sockets, try again.", e);
                                    // 
                                    trySucceeded = false;
                                }
                            }
                            if (tries == 10) 
                            {
                                JBButil.getLog().warning(
                                    "10 failed socket connection attempts. Exiting..");
                                return;
                            }
                            try 
                            {
                                socIn = new BufferedReader(new InputStreamReader(soc
                                    .getInputStream()));
                            }
                            catch (Exception e) 
                            {
                                JBButil.getLog().log(Level.WARNING,
                                    e + ": error in setting socket input", e);
                                return;
                            }
                            try 
                            {
                                socOut = new PrintWriter(new OutputStreamWriter(soc
                                    .getOutputStream()));
                            }
                            catch (Exception e) 
                            {
                                JBButil.getLog().log(Level.WARNING,
                                    e + ": error in setting socket out", e);
                                return;
                            }
                        }*/ //for multi jvm's and I don;t think we need it for CLR.
                        // CORECLR Console.Out.Flush(); //fffffffffffffffffffffffffffffffff

                        main.doIt();
                        // CORECLR Console.Out.Flush(); //fffffffffffffffffffffffffffffffff
                        // And again right after we are finished
                        main.flushLog();
                        if (JBBProperties.printPropertiesAndArgs)
                        {
                            // Puts sorted System Properties into Raw file
                            IDictionary id = Environment.GetEnvironmentVariables();
                            //SortedList id = (SortedList)Environment.GetEnvironmentVariables();
                            //Properties props = System.getProperties();
                            //Set keys = props.keySet();
                            //Vector keyvec = new Vector(keys);
                            //Collections.sort(keyvec);
                            /*
                            for (int i = 0; i < keyvec.size(); i++) 
                            {
                                String propsKey = (String) keyvec.elementAt(i);
                                String svalue = props.getProperty(propsKey);
                                outRawFile.WriteLine(propsKey + "=" + svalue);
                            }*/

                            foreach (DictionaryEntry de in id)
                            {
                                String sname = (String)de.Key;
                                String svalue = (String)de.Value; //no need to call GetEnvironmentVariabe again correct.
                                outRawFile.WriteLine("{0}={1}", (string)sname, svalue);
                            }
                            /*
                            for (int i=0; i < id.Keys.Count; i++)
                            {
                                //String svalue = System.getProperties().getProperty((string) snames[i]);
                                String sname = (String)id.GetKeyList()[i];
                                String svalue = Environment.GetEnvironmentVariable(sname);
                                //outRawFile.WriteLine((string) snames[i] + "=" + svalue);
                                outRawFile.WriteLine("{0}={1}" ,(string) sname, svalue);
                            }*/
                            // Puts command line args into raw file
                            outRawFile.Write("input.cmdline=");
                            for (int i = 0; i < args.Length; i++)
                            {
                                //outRawFile.Write(args[i] + " ");
                                outRawFile.Write("{0} ", args[i]);
                            }
                            outRawFile.WriteLine();
                        }
                        //Add by Milind...
                        // outRawFile.Close(); //Reporter tries to load the file in a stream and so we need to close this

                        //before reporter can make use of it.
                        //End
                        // UBUNTU
                        Console.WriteLine("Results appended to results/results.txt");
                        // main.callReporter(output_directory, outRawFile_name, outRawPrefix,
                        //    sequenceNumber);
                        // CORECLR outDeliveriesFile.Close();
                        // CORECLR outRawFile.Close();
                    }
                }
                // CORECLR elian: sending FINISHED state
                /*
				if (multiJVMMode) 
				{
					String msg = JBBmain.instanceId + ":FINISHED";
					String exitMsg = JBBmain.instanceId + ":EXIT";
					JBButil.getLog().info(msg);
					Console.WriteLine(msg);
					JBBmain.socOut.println(msg);
					Console.WriteLine("Sent FINISHED message");
					JBBmain.socOut.flush();
					String mesg = "NULL";
					try 
					{
						while ((mesg != null) && !mesg.matches(exitMsg))
							mesg = JBBmain.socIn.readLine();
					}
					catch (NullPointerException e)
					{
						//do nothing
					}
					catch(java.net.SocketException se)
					{
						//do nothing here too
					}
					catch (Exception e)
					{
						JBButil.getLog().log(Level.WARNING,
							e + ": error awaiting final exit message", e);
						return;
					}
					Console.WriteLine("Final EXIT reached");
				}*/
                SaveOutput.stop();
                File.Delete(logFile_validation_name); //Added by Milind. Just deletes the validationlog.txt
            }
            finally
            {
                // Logging has now been initialized. Log this event. Check.This.One.Later
                //JBButil.getLog().info("Logging ended");
                //Handler[] handlers = JBButil.getLog().getHandlers();
                // Close all logging handlers
                //for (Handler logHandler : handlers) 
                //{
                //	logHandler.close();
                //}
            }
            //outResultsFile.close();
            // CORECLR Environment.Exit(0) ;
        }//Main
         /*
         private bool initApplicationLogging_forValidation(String logFileName) 
         {

             FileStream handler = null;
             try 
             {
                 handler =  new FileStream(logFileName,FileMode.Create,FileAccess.Write);
             }
             catch (FileNotFoundException fnfe) 
             {
                 Trace.WriteLineIf(JBButil.getLog().TraceWarning,
                     "ERROR:  Unable to open logging file " + logFileName);
                 return false;
             }
             Trace.Listeners.Clear();
             Trace.Listeners.Add(new TextWriterTraceListener(handler));
             TraceSwitch ts = new TraceSwitch("TestSwitch", "SPECjbb.Switch"); 
             ts.Level = TraceLevel.Info;//prop.getApplicationLoggingLevel();

             Trace.AutoFlush = true;
             JBButil.setLog(ts);
             return true;
         }
         */
        private bool initApplicationLogging(String logFileName, bool Validation)
        {
            FileStream handler = null;
            //StreamWriter handler = null;

            try
            {
                handler = new FileStream(logFileName, FileMode.Create, FileAccess.ReadWrite);
                //handler = new StreamWriter(logFileName);
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine("ERROR:  Unable to open logging file " + fnfe.Message + " " + logFileName);
                return false;
            }
            Trace.Listeners.Clear();
            // Trace.Listeners.Add(new TextWriterTraceListener(handler, "Spec.jbb"));
            TraceSwitch ts = new TraceSwitch("TestSwitch", "SPECjbb.Switch");
            if (Validation == true) //for the validation just go the finest level which is VERBOSE.
                ts.Level = TraceLevel.Verbose;
            else
                ts.Level = prop.getApplicationLoggingLevel();

            Trace.AutoFlush = true;
            JBButil.setLog(ts);
            return true;
        }

        private void flushLog()
        {
            Trace.Flush();
            /*
			Handler[] handlers = JBButil.getLog().getHandlers();
			// Close all logging handlers
			for (Handler logHandler : handlers) 
			{
				logHandler.flush();
			}
			*/
        }
    }
}

