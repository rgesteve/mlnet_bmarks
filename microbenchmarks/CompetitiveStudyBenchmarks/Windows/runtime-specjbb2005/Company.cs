using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Company.
	/// </summary>
	public class Company
	{

		// This goes right after each class/interface statement
		//  internal static readonly String        COPYRIGHT                  = "SPECjbb2005,"
		//  	+ "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  	+ "All rights reserved,"
		//  	+ "(C) Copyright IBM Corp., 1996 - 2005"
		//  	+ "All rights reserved,"
		//  	+ "Licensed Materials - Property of SPEC";

		static private StreamWriter outPropFile;

		static private StreamWriter outDeliveriesFile;

		private String             propPrefix                 = null;

		private Object[]           warehouseTable;

		private JBBDataStorage     customerTable;

		private JBBSortedStorage   lastNameCustomerTable;

		private JBBDataStorage     itemTable;

		private Object[]           timerdataTable;

		private Object[]           warehouseContainers;

		// timing variables:
		private long               rampup_time;

		private long               measurement_time;

		private TimerData          companyTimerData;

		private long               elapsed_time;

		// population variables:
		private short              PreviousMaxWarehouses;                                                       // holds

		// number of warehouses for last run
		private short              MaxWarehouses = 0;                                                               // this

		private short              MaxDistrictsPerWarehouse;                                                    // should

		private int                MaxCustomersPerDistrict;                                                     // should

		private int                MaxItems;                                                                    // should

		private int                MaxStock;                                                                    // should

		private int                InitialOrders;                                                               // should

		private int                InitialNewOrders;                                                            // should

		private int                InitialHistories;                                                            // should

		private int                warehouseCapacity          = JBBmain.maxWh;                                   

		private ArrayList          warehouseThreads;

		private long               jvm_instances              = 0;

		private float              per_jvm_warehouse_rampup   = 0.5f;

		private float              per_jvm_warehouse_rampdown = 0.5f;

        // Should really make these private and provide "getters".
        internal readonly Object initThreadsStateChange = new object();

        internal Object              initThreadsCountMonitor    = null;

		internal int                 initThreadsCount           = 0;

		internal Object              threadsDoneCountMonitor    = null;

		internal int                 threadsDoneCount           = 0;

		internal Object              stopThreadsCountMonitor    = null;

		internal int                 stopThreadsCount           = 0;

        private readonly object _syncRoot = new Object(); // CORECLR

        public enum runModes 
		{
			DEFAULT_MODE, MULTI_RAMP, RAMP_UP, RECORDING, RAMP_DOWN, STOP
		};

		private volatile runModes mode = runModes.DEFAULT_MODE;

		public static void setOutputs(StreamWriter oPropFile, StreamWriter oDeliveriesFile) 
		{
			outPropFile = oPropFile;
			outDeliveriesFile = oDeliveriesFile;
		}//setOutputs

		

		public Company()
		{
			JBButil.random_init(warehouseCapacity);
			Console.WriteLine("Constructing the company now   Hang....on");
			Console.WriteLine("");
			warehouseContainers = new Object[warehouseCapacity];
			warehouseTable = new Object[warehouseCapacity]; // was new Hashtable();
			customerTable = Infrastructure.createStorage();
			lastNameCustomerTable = Infrastructure.createSortedStorage();
			itemTable = Infrastructure.createStorage();
			timerdataTable = new Object[warehouseCapacity];
			companyTimerData = new TimerData();
			MaxWarehouses = 0;
			initThreadsStateChange = new Object();
			initThreadsCountMonitor = new Object();
			initThreadsCount = 0;
			threadsDoneCountMonitor = new Object();
			threadsDoneCount = 0;
			stopThreadsCountMonitor = new Object();
			stopThreadsCount = 0;
			// add ArrayList to save pointers to warehouse threads
			warehouseThreads = new System.Collections.ArrayList(warehouseCapacity);
		}//Company constructor.

		public StreamWriter getOutDeliveriesFile() 
		{
			return outDeliveriesFile;
		}//

		public short getMaxWarehouses() 
		{
			return MaxWarehouses;
		}

		public short getMaxDistrictsPerWarehouse() 
		{
			return MaxDistrictsPerWarehouse;
		}

		public int getMaxCustomersPerDistrict() 
		{
			return MaxCustomersPerDistrict;
		}

		public int getMaxItems() 
		{
			return MaxItems;
		}

		public int getInitialOrders() 
		{
			return InitialOrders;
		}

		public int getInitialNewOrders() 
		{
			return InitialNewOrders;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void primeWithDummyData(short number_of_warehouses, int choice) 
		{
            lock (_syncRoot)
            {
                switch (choice)
                {
                    case 0:
                        {
                            PreviousMaxWarehouses = MaxWarehouses;
                            if (PreviousMaxWarehouses == 0)
                                MaxWarehouses = number_of_warehouses;
                            else
                                ++MaxWarehouses;
                            String msg = "Loading Warehouse " + MaxWarehouses + "...";
                            Console.WriteLine(msg);
                            Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
                                                                               // Item Table must be loaded first since the warehouses use it
                                                                               // for
                                                                               // construction
                            if (PreviousMaxWarehouses == 0)
                            {
                                loadItemTable();
                            }
                            loadWarehouseTable();
                            loadCustomerTable();
                            loadWarehouseHistoryTable();
                            loadInitialOrders();
                            Console.WriteLine("");
                            Console.WriteLine("");
                        }
                        break;
                    case 1:
                        {
                            PreviousMaxWarehouses = MaxWarehouses;
                            if (PreviousMaxWarehouses == 0)
                                MaxWarehouses = number_of_warehouses;
                            else
                                ++MaxWarehouses;
                            String msg = "Loading Warehouse " + MaxWarehouses + "...";
                            Console.WriteLine(msg);
                            Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
                                                                               // Item Table must be loaded first since the warehouses use it
                                                                               // for
                                                                               // construction
                            if (PreviousMaxWarehouses == 0)
                            {
                                loadItemTable();
                            }
                        }
                        break;
                    case 2:
                        {
                            loadWarehouseTable();
                        }
                        break;
                    case 3:
                        {
                            loadCustomerTable();
                        }
                        break;
                    case 4:
                        {
                        }
                        break;
                    case 5:
                        {
                            loadInitialOrders();
                            Console.WriteLine("");
                            Console.WriteLine("");
                        }
                        break;
                }
            }
		} //primeWithDummyData

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void startAutomated(short inWarehouseId,
            int rampup_time, int measurement_time)
        {
            lock (_syncRoot)
            {
                companyTimerData.zeroTimerData();
                this.rampup_time = rampup_time;
                this.measurement_time = measurement_time;
                TimerData warehouseTimerData = getTimerDataPtr(inWarehouseId);
                warehouseTimerData.zeroTimerData();
                warehouseTimerData.setRampUpTime(rampup_time);
                warehouseTimerData.setMeasurementTime(measurement_time);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void startValidation(short inWarehouseId)
        {
            lock (_syncRoot)
            {
                companyTimerData.zeroTimerData();
                TimerData warehouseTimerData = getTimerDataPtr(inWarehouseId);
                warehouseTimerData.zeroTimerData();
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void addWarehouseThread(TransactionManager tm)
        {
            lock (_syncRoot)
            {
                warehouseThreads.Add(tm);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void prepareForStart()
        {
            lock (_syncRoot)
            {
                initThreadsCount = 0;
                threadsDoneCount = 0;
                stopThreadsCount = 0;
                mode = runModes.DEFAULT_MODE;
                warehouseThreads.Clear();
            }
        }
		public long getElapsedTime() 
		{
			return elapsed_time;
		}

		// return with write lock if lockFlag is True
		public Warehouse getWarehousePtr(short warehouseId, bool lockFlag) 
		{
			// index lookup get write lock if lockFlag is True
			Warehouse result;
			result = (Warehouse) warehouseTable[warehouseId];
			return result;
		}

		// return with write lock if lockFlag is True
		public Customer getCustomer(long customerId, bool lockflag) 
		{
			// index lookup gets write lock
			Customer result;
			result = (Customer) customerTable.get(customerId);
			//if (JBButil.getLog().isLoggable(Level.FINEST)) 
			//if(JBButil.getLog().Level >= TraceLevel.Verbose)
			//{
			TraceSwitch log = JBButil.getLog();
			Trace.WriteLineIf(log.TraceVerbose,"Company::getCustomer");//log.finest("Company::getCustomer");
			Trace.WriteLineIf(log.TraceVerbose,"  customerId=" + customerId); //log.finest("  customerId=" + customerId);
			Trace.WriteLineIf(log.TraceVerbose,"  Customer=" + result);//log.finest("  Customer=" + result);
			//}
			return result;
		}

		public bool isCustomer(long customerId) 
		{
			return customerTable.containsKey(customerId);
		}

		public long buildUniqueCustomerKey(short warehouseId, sbyte districtId,
			short customerId) 
		{
			// warehouseId=1:12, districtId=13:24, customerId=25-64
			long key = warehouseId;
			key = key << 12;
			key += districtId;
			key = key << 40;
			key += customerId;
			return key;
		}

		// return customer with a write lock if lockFlag is True
		public Customer getCustomerByLastName(short warehouseId, sbyte districtId,
			String last_name) 
		{
			String custKey = ((int)warehouseId).ToString() + "_" + 
				((int)districtId).ToString() + "_" + last_name ;  

			String lastCustKey = custKey + "_~";
            //Milind for debugging purposes on Oct 17th
            //Console.WriteLine("getCustomerByLastName:custKey={0}    lastCustKey={1}", custKey, lastCustKey);

			long custId = (long) lastNameCustomerTable.getMedianValue(custKey,
				lastCustKey);
			return (Customer) customerTable.get(custId);
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setJVMInstanceValues(long instanceCount)
        {
            lock (_syncRoot)
            {
                this.jvm_instances = instanceCount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setMultiJVMRampingValues(float rampup, float rampdown)
        {
            lock (_syncRoot)
            {
                this.per_jvm_warehouse_rampup = rampup;
                this.per_jvm_warehouse_rampdown = rampdown;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setPopulationValues(int population_base)
        {
            lock (_syncRoot)
            {
                if (population_base > 0)
                {
                    MaxDistrictsPerWarehouse = 10;
                    MaxCustomersPerDistrict = population_base;
                    if (JBBmain.overrideItemTableSize > 0)
                    {
                        MaxItems = JBBmain.overrideItemTableSize;
                    }
                    else
                    {
                        MaxItems = 20000;
                    }
                }
                else
                {
                    // minimally populated databases (used for quicker startup &
                    // turnaround during testing)
                    MaxDistrictsPerWarehouse = 5;
                    MaxCustomersPerDistrict = 30;
                    MaxItems = 100;
                }
                MaxStock = MaxItems;
                InitialOrders = MaxCustomersPerDistrict;
                InitialNewOrders = (short)(MaxCustomersPerDistrict * 0.30);
                InitialHistories = MaxCustomersPerDistrict;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setPopulationValues(int population_base,
            int itemtable_size)
        {
            lock (_syncRoot)
            {
                if (population_base > 0)
                {
                    MaxDistrictsPerWarehouse = 10;
                    MaxCustomersPerDistrict = population_base;
                    MaxItems = itemtable_size;
                }
                else
                {
                    // minimally populated databases (used for quicker startup &
                    // turnaround during testing)
                    MaxDistrictsPerWarehouse = 5;
                    MaxCustomersPerDistrict = 30;
                    MaxItems = 100;
                }
                MaxStock = MaxItems;
                InitialOrders = MaxCustomersPerDistrict;
                InitialNewOrders = (short)(MaxCustomersPerDistrict * 0.30);
                InitialHistories = MaxCustomersPerDistrict;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public TimerData getTimerDataPtr(short warehouseId)
        {
            lock (_syncRoot)
            {
                TimerData temp;
                // System.out.println("warehouseID: " + warehouseId);
                // new Exception().printStackTrace();
                if (warehouseId == 0)
                    temp = companyTimerData;
                else
                    temp = (TimerData)timerdataTable[warehouseId];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void trimOrdersForSteadyState()
        {
            lock (_syncRoot)
            {
                short warehouseId;
                Warehouse warehousePtr;
                int initialOrders = this.getInitialOrders();
                int initialNewOrders = this.getInitialNewOrders();
                //Milind on 03/07/2007
                lastNameCustomerTable.setKeyListToNull();
                // print results
                for (warehouseId = 1; warehouseId <= MaxWarehouses; warehouseId++)
                {
                    warehousePtr = getWarehousePtr(warehouseId, false); // protected by
                    warehousePtr.trimOrdersForSteadyState(initialOrders, initialNewOrders);
                }
            }
        }
		public void displayResultTotals(bool showWarehouseDetail) 
		{
            
            short warehouseId;
			TimerData warehouseTimerData;

            lock (initThreadsStateChange)
            {
                while (initThreadsCount != MaxWarehouses)
                {
                    try
                    {
                        Monitor.Wait(initThreadsStateChange);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: " + e.Message + "\r\n" + e.StackTrace);
                    }
                }
            }

            // Tell everybody it's time for warmups.
            setrunMode(runModes.RAMP_UP);
			lock(initThreadsCountMonitor) 
			{
				Monitor.PulseAll(initThreadsCountMonitor) ;
				//initThreadsCountMonitor.notifyAll();
			}
			String msg;
			long start_time = 0;
			long end_time = 0;
			
			if (rampup_time > 0) 
			{
				msg = "User Thread Rampup began " + DateTime.Now.ToString() + " for "
					+ /*df.format*/(rampup_time / 60.00) + " minutes";

				Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
				Console.WriteLine(msg); // display rampup start time
				JBButil.SecondsToSleep((int) rampup_time);
			}
			if (measurement_time > 0) 
			{
				msg = "Timing Measurement began " + DateTime.Now.ToString() + " for "
					+ /*df.format*/(measurement_time / 60.00) + " minutes";
				setrunMode(runModes.RECORDING);
				start_time = Environment.TickCount ;//System.currentTimeMillis();
				Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
				Console.WriteLine(msg); // display start time
				// Console.Out.Flush();
				// Wait while user threads do the recorded run
				JBButil.SecondsToSleep((int) measurement_time);
			}
			end_time = Environment.TickCount ;//System.currentTimeMillis();
			msg = "Timing Measurement ended " + DateTime.Now.ToString() ;
			// Console.Out.Flush();
			Trace.WriteLineIf(JBButil.getLog().TraceInfo, msg);//JBButil.getLog().info(msg);
			Console.WriteLine(msg); // display stop time
			
			setrunMode(runModes.RAMP_DOWN);
			elapsed_time = end_time - start_time;
			Console.WriteLine("");
                  
			lock(threadsDoneCountMonitor) 
			{
				while (threadsDoneCount != MaxWarehouses) 
				{
					try 
					{
						Monitor.Wait(threadsDoneCountMonitor) ;
					}
					catch (Exception e) 
					{
                        Console.WriteLine(e.Message);
                    }
                }
			}
                  
			setrunMode(runModes.STOP);
			
			lock(stopThreadsCountMonitor) 
			{
				while (stopThreadsCount != MaxWarehouses) 
						Monitor.Wait(stopThreadsCountMonitor) ;
			}
			// print results
			outPropFile.WriteLine(propPrefix + "warehouses=" + MaxWarehouses);
			/*
			if (JBBmain.multiJVMMode) 
			{
				outPropFile.WriteLine(propPrefix + "start_rampup_time_milliseconds="
					+ start_rampup_time);
				outPropFile.WriteLine(propPrefix + "end_rampdown_time_milliseconds="
					+ end_rampdown_time);
			}
			*/
			outPropFile.WriteLine(propPrefix + "start_time_milliseconds=" + start_time);
			outPropFile.WriteLine(propPrefix + "end_time_milliseconds=" + end_time);
			outPropFile.WriteLine(propPrefix + "elapsed_milliseconds=" + elapsed_time);
			Console.WriteLine("");
			int total_warehouse_trans = 0;
			long min_transaction_count = long.MaxValue;
			long max_transaction_count = long.MinValue;
			for (warehouseId = 1; warehouseId <= MaxWarehouses; warehouseId++) 
			{
				// System.out.print("\nTOTALS FOR WAREHOUSE " + warehouseId + ":");
				warehouseTimerData = getTimerDataPtr(warehouseId);
				warehouseTimerData.calculateResponseTimeStats();
				// CJB 2001/11/01: warehouse data is not used when calculating
				// results,
				// and all this data is causing the results file to get too large,
				// so we are making it optional.
				if (showWarehouseDetail)
					warehouseTimerData.propResults(propPrefix + "warehouse_" + warehouseId + ".", outPropFile);
				total_warehouse_trans = 0;
				for (int txntype = 0; txntype < Transaction.maxTxnTypes; txntype++) 
				{
					total_warehouse_trans += (int)warehouseTimerData.getTransactionCount(txntype);
				}
				if (total_warehouse_trans < min_transaction_count) 
				{
					min_transaction_count = total_warehouse_trans;
				}
				if (total_warehouse_trans > max_transaction_count) 
				{
					max_transaction_count = total_warehouse_trans;
				}
			}
			// System.out.print("\n\n\nTOTALS FOR COMPANY:");
			Console.WriteLine("Calculating results");
			// Console.Out.Flush();
			companyTimerData.calculateResponseTimeStats();
			companyTimerData.displayThreadResults();
			long diff = max_transaction_count - min_transaction_count;
			float diff_pct = 100 * (float) diff / (float) max_transaction_count;
			Console.WriteLine("");
			Console.WriteLine("Minimum transactions by a warehouse = "
				+ min_transaction_count);
			Console.WriteLine("Maximum transactions by a warehouse = "
				+ max_transaction_count);
			Console.WriteLine("Difference (thread spread) = " + diff + " (" + "{0:####.00}" + "%)",diff_pct);
			Console.WriteLine("");
			// Console.Out.Flush();
			companyTimerData.displayResults(
				("COMPANY with " + MaxWarehouses + " warehouses "), JBButil
				.currentTotalMem(), JBButil.currentFreeMem());
			companyTimerData.propResults(propPrefix + "company.", outPropFile,
				JBButil.currentTotalMem(), JBButil.currentFreeMem());
			companyTimerData
				.propThreadResults(propPrefix + "company.", outPropFile);
			outPropFile.WriteLine(propPrefix + "company.min_warehouse_transactions="
				+ min_transaction_count);
			outPropFile.WriteLine(propPrefix + "company.max_warehouse_transactions="
				+ max_transaction_count);
			if (Transaction.steadyStateMem) 
			{
				trimOrdersForSteadyState();
			}
			setrunMode(runModes.DEFAULT_MODE);
		} //displayResultsTotal

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadWarehouseTable()
        {
            lock (_syncRoot)
            {
                for (short i = (short)(PreviousMaxWarehouses + 1); i <= MaxWarehouses; ++i)
                {
                    // WRR: Pass warehouseId here so Random stream can be registered.
                    Warehouse newWarehouse = new Warehouse();
                    newWarehouse.initWarehouse(this, itemTable, i);
                    newWarehouse.setUsingRandom(i);
                    warehouseTable[i] = newWarehouse;
                    TimerData newTimerData = new TimerData();
                    timerdataTable[i] = newTimerData;
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadCustomerTable()
        {
            lock (_syncRoot)
            {
                // System.out.println("MaxCustomers = " + MaxDistrictsPerWarehouse *
                // MaxCustomersPerDistrict);
                short customerId;
                long customers_loaded = 0;
                // go through all of the warehouses
                for (short warehouseId = (short)(PreviousMaxWarehouses + 1); warehouseId <= MaxWarehouses; ++warehouseId)
                {
                    // go through all districts
                    for (sbyte districtId = 1; districtId <= MaxDistrictsPerWarehouse; ++districtId)
                    {
                        // go through and create customers for each district
                        for (customerId = 1; customerId <= MaxCustomersPerDistrict; ++customerId)
                        {
                            // Customer newCustomer = Customer.createCustomer(this,
                            // null);
                            Customer newCustomer = new Customer();
                            newCustomer.setUsingRandom(customerId, warehouseId, districtId);
                            long uniqueCustomerNumber = buildUniqueCustomerKey(warehouseId, districtId, customerId);
                            customerTable.put(uniqueCustomerNumber, newCustomer);
                            String custNameKey = ((int)warehouseId).ToString()
                                + "_" + ((int)districtId).ToString() + "_"
                                + newCustomer.getLastName() + "_"
                                + ((int)customerId).ToString();
                            //Milind for debugging purposes on Oct 17th
                            //Console.WriteLine("loadCustomerTable:custNameKey={0}   lastName={1}", custNameKey, newCustomer.getLastName());
                            lastNameCustomerTable.put(custNameKey, uniqueCustomerNumber);
                            //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                            //if(JBButil.getLog().Level >= TraceLevel.Verbose)
                            //{
                            TraceSwitch log = JBButil.getLog();
                            Trace.WriteLineIf(log.TraceVerbose, "Company::loadCustomerTable");
                            Trace.WriteLineIf(log.TraceVerbose, "  newCustomer=" + newCustomer);
                            Trace.WriteLineIf(log.TraceVerbose, "  customerId=" + customerId);
                            Trace.WriteLineIf(log.TraceVerbose, "  districtId=" + districtId);
                            Trace.WriteLineIf(log.TraceVerbose, "  warehouseId=" + warehouseId);
                            Trace.WriteLineIf(log.TraceVerbose, "  uniqueCustomerNumber="
                                + uniqueCustomerNumber);
                            Trace.WriteLineIf(log.TraceVerbose, "  custNameKey=" + custNameKey);
                            //}
                        }
                        customers_loaded += customerId - 1;
                    }
                }
            }
        }
		public void setrunMode(runModes inmode) 
		{
			// first set per-warehouse run-mode
			TransactionManager tm;
			for (int i = 0; i < warehouseThreads.Count; i++) 
			{
				tm = (TransactionManager) warehouseThreads[i];
				tm.setrunMode(inmode);
			}
			mode = inmode;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadItemTable()
        {
            lock (_syncRoot)
            {
                for (int i = 1; i <= MaxItems; ++i)
                {
                    Item anItem = new Item();
                    anItem.setUsingRandom(i);
                    itemTable.put(i, anItem);
                }
            }
        }
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void dumpWarehouseTable() 
		{
		}
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void dumpCustomerTable() 
		{
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void dumpItemTable() 
		{
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadWarehouseHistoryTable()
        {
            lock (_syncRoot)
            {
                for (short i = (short)(PreviousMaxWarehouses + 1); i <= MaxWarehouses; ++i)
                {
                    ((Warehouse)warehouseTable[i]).loadHistoryTable();
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void loadInitialOrders()
        {
            lock (_syncRoot)
            {
                // go through all of the warehouses
                for (short warehouseId = (short)(PreviousMaxWarehouses + 1); warehouseId <= MaxWarehouses; ++warehouseId)
                {
                    NewOrderTransaction newOrderTransaction = new NewOrderTransaction(
                        this, warehouseId);
                    // go through all of the districts for each warehouse
                    for (sbyte districtId = 1; districtId <= MaxDistrictsPerWarehouse; ++districtId)
                    {
                        // go through all of the customers for each district
                        for (short customerId = 1; customerId <= MaxCustomersPerDistrict; ++customerId)
                        {
                            newOrderTransaction.init();
                            newOrderTransaction.setDistrictandCustomer(districtId,
                                customerId);
                            newOrderTransaction.processPreloadedOrders();
                        }
                    }
                }
            }
        }
		public void setPropOutputPrefix(String s) 
		{
			propPrefix = s;
		}
	}//public class company
}