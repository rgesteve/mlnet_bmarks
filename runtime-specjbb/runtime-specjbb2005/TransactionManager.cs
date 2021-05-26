/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for TransactionManager.
	/// </summary>
	public class TransactionManager
	{
		// This goes right after each class/interface statement
		//  static readonly String       COPYRIGHT       = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996-2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Company           company;

		private short             warehouseId;                                                      // W_ID

		public Company.runModes   mode            = Company.runModes.DEFAULT_MODE;

		private static readonly sbyte new_order       = Transaction.new_order;

		private static readonly sbyte payment         = Transaction.payment;

		private static readonly sbyte order_status    = Transaction.order_status;

		private static readonly sbyte delivery        = Transaction.delivery;

		private static readonly sbyte stock_level     = Transaction.stock_level;

		private static readonly sbyte cust_report     = Transaction.cust_report;

		private static readonly sbyte maxTxnTypes     = Transaction.maxTxnTypes;

		private static readonly sbyte multiple_orders = maxTxnTypes;

		private static readonly sbyte pgm_exit        = (sbyte) (multiple_orders + 2);

		private Transaction[]       transactionInstance;

		public void initTransactionManager(Company inCompany, short warehouseId) 
		{
			company = inCompany;
			this.warehouseId = warehouseId;
			// This is necessary to keep sequence of random calls
			JBButil.random(1, company.getMaxDistrictsPerWarehouse(), warehouseId);
		}

		// elian061004: transactions creation via reflection
		private void createTxnInstances() 
		{
            // CORECLR
			transactionInstance = new Transaction[maxTxnTypes];
			//for (int i = 0; i < maxTxnTypes; ++i) 
			//{
			//	transactionInstance[i] = Transaction.getInstance(
			//		Transaction.transactionClasses[i], company, warehouseId);
			//}
            int i = 0;
            foreach (Transaction.TransactionTypes txnType in Enum.GetValues(typeof(Transaction.TransactionTypes)))
            {
                transactionInstance[i] = Transaction.GetTransactionInstance(txnType, company, warehouseId);
                i++;
            }
        }

		private void manualSelection() 
		{
			int i;
			Console.WriteLine("Select transaction type");
			Console.WriteLine();
			for (i = 0; i < maxTxnTypes; ++i) 
			{
				try 
				{
                    Console.WriteLine((i + 1)
                               + ". "
                               // CORECLR + Transaction.transactionClasses[i].GetMethod("getMenuName", (Type[]) null).Invoke(null,(Object[]) null));
                               // CORECLR TODO implement Getmethod alternative
                               );
				}
				catch (Exception e) 
				{
					Trace.WriteLineIf(JBButil.getLog().TraceWarning,
						"TransactionManager.manualSelection - "
						+ "NoSuchMethodException, or IllegalAccessException" + e.Message);
				}
			}
			Console.WriteLine(multiple_orders + 1 + ". Create NewOrders");
			Console.WriteLine(pgm_exit + 1 + ". Exit");
			Console.WriteLine();
			Console.WriteLine("Enter selection here: ");
		}

		private String readUserValue() 
		{
			//StreamReader keyboard_input;
			String s = "";
			try 
			{
				//keyboard_input = new StreamReader(System.Console.In);
				s = Console.In.ReadLine();//keyboard_input.ReadLine();
			}
			catch (IOException e) 
			{
				Trace.WriteLineIf(JBButil.getLog().TraceWarning,
					"TransactionManager.readUserValue - ",
					e.Message);
			}
			return s;
		}

		// elian061004: runs processes for the given transaction type
		// with the defined pauses
		private long runTxn(Transaction txn, long menuWaitTime,
			long typingWaitTime, double thinkingWaitTime) 
		{
			long start;
			long end;
			txn.init();
			if (menuWaitTime > 0) 
			{
				JBButil.milliSecondsToSleep(menuWaitTime);
			}
			txn.initializeTransactionLog();
			if (typingWaitTime > 0) 
			{
				JBButil.milliSecondsToSleep(typingWaitTime);
			}
			start = Environment.TickCount;//System.currentTimeMillis(); // get start time
			txn.process();
			txn.processTransactionLog();
			end = Environment.TickCount;//System.currentTimeMillis(); // get end time
			if (thinkingWaitTime > 0.0) 
			{
				JBButil.SecondsToSleep(thinkingWaitTime);
			}
			return (end - start);
		}

		// elian061004: does different actions based on manual selection value
		private long goManual(int selection, TimerData myTimerData) 
		{
			long menuWaitTime = 0;
			long typingWaitTime = 0;
			double thinkingWaitTime = 0.0;
			if (isMultiple(selection)) 
			{
				// special case processing for multiple ...
				long numOrders = 0;
				int i;
				Console.WriteLine("How many orders to be created? ");
				numOrders = int.Parse(readUserValue());//new Integer(readUserValue()).intValue();
				Console.WriteLine("Creating New Orders...");
				for (i = 0; i < numOrders; ++i) 
				{
					// Instance of NewOrderTransaction
					transactionInstance[0].init();
					transactionInstance[0].process();
				}
				return 0;
			}
			else 
			{
				if (selection < maxTxnTypes) 
				{
					menuWaitTime = myTimerData.getMenuWaitTime(selection);
					typingWaitTime = myTimerData.getTypingWaitTime(selection);
					thinkingWaitTime = JBButil
						.negativeExpDistribution(((double) myTimerData
						.getThinkingWaitTime(selection)) / 1000.0D,
						warehouseId);
				}
				return runTxn(transactionInstance[selection], menuWaitTime,
					typingWaitTime, thinkingWaitTime);
			}
		}

		bool isMultiple(int selection) 
		{
			if (selection == multiple_orders) 
			{
				return true;
			}
			else 
			{
				return false;
			}
		}
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Company.runModes getrunMode() 
		{
			return mode;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setrunMode(Company.runModes inmode)
        {
            lock (_syncRoot)
            {
                mode = inmode;
            }
        }
		public void go() 
		{
			sbyte co = 0;
			int[] deck = new int[33];
			long elapsed_time;
			long txntime;
			int txntype;
			bool timed = false;
			bool signaled_done = false;
			int i = 0;
			TimerData warehouseTimerDataPtr = company.getTimerDataPtr(warehouseId);
			long rampup_time = warehouseTimerDataPtr.getRampUpTime();
			long measurement_time = warehouseTimerDataPtr.getMeasurementTime();
			// create object to store timer data for this process
			TimerData myTimerData = new TimerData();
			// copy wait times from warehouseTimerData to myTimerData
			myTimerData.setWaitTimes(warehouseTimerDataPtr.getWaitTimes());
			deck = buildDeck();
			Warehouse warehousePtr = company.getWarehousePtr(warehouseId, false);
			// create transaction objects near their warehouse
			createTxnInstances();
			lock (company.initThreadsCountMonitor) 
			{
				lock (company.initThreadsStateChange) 
				{
					company.initThreadsCount++;
					Monitor.Pulse(company.initThreadsStateChange);//company.initThreadsStateChange.notify();
				}
				try 
				{
					Monitor.Wait(company.initThreadsCountMonitor);//company.initThreadsCountMonitor.wait();
				}
				catch (Exception ex) 
				{
                    Console.WriteLine(ex.Message);
				}
			}
			if ((rampup_time > 0) || (measurement_time > 0)) 
			{
				timed = true;
			}
			//if (JBButil.getLog().isLoggable(Level.FINEST)) 
			//{
				Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"Benchmark " + JBBmain.Version + ": warehouse "
					+ warehouseId);
			//}
			while (this.getrunMode() != Company.runModes.STOP) 
			{
				if ((!timed)&& (this.getrunMode() == Company.runModes.DEFAULT_MODE)) 
				{
					manualSelection();
					txntype = int.Parse(readUserValue())-1 ;//(new Integer(readUserValue()).intValue()) - 1;
				}
				else 
				{
					txntype = deck[i];
					i++;
					if (i == 33) 
					{
						deck = buildDeck();
						i = 0;
					}
				}
				txntime = goManual(txntype, myTimerData);
				if (this.getrunMode() == Company.runModes.RECORDING)
					myTimerData.updateTimerData(txntype, txntime);

				if (timed) 
				{
					if ((this.getrunMode() == Company.runModes.RAMP_DOWN) && (!signaled_done)) 
					{
						//Console.Out.Flush();
						lock (company.threadsDoneCountMonitor) 
						{
                                          company.threadsDoneCount++;
							Monitor.Pulse(company.threadsDoneCountMonitor);//company.threadsDoneCountMonitor.notify();
							signaled_done = true;
						}
					}
				}
				else 
				{
					if (txntype == pgm_exit) 
					{
						break;
					}
				}
			}
			if (timed && (this.getrunMode() == Company.runModes.STOP)) 
			{
				elapsed_time = company.getElapsedTime();
				myTimerData.calculateResponseTimeStats();
				double tpmc = myTimerData.updateTPMC(elapsed_time);
				double btps = myTimerData.updateBTPS(elapsed_time);
				// roll up totals to warehouse and company
				long totalTransactions = 0;
				for (txntype = 0; txntype < maxTxnTypes; txntype++) 
				{
					warehouseTimerDataPtr.rollupTimerData(txntype, myTimerData
						.getTransactionCount(txntype), myTimerData
						.getTotalTime(txntype), myTimerData
						.getTotalTimeSquare(txntype), myTimerData
						.getMinimumTime(txntype), myTimerData
						.getMaximumTime(txntype));
					company.getTimerDataPtr(co).rollupTimerData(txntype,
						myTimerData.getTransactionCount(txntype),
						myTimerData.getTotalTime(txntype),
						myTimerData.getTotalTimeSquare(txntype),
						myTimerData.getMinimumTime(txntype),
						myTimerData.getMaximumTime(txntype));
					totalTransactions += myTimerData.getTransactionCount(txntype);
				}
				company.getTimerDataPtr(co).accumulateTransactionStats(
					totalTransactions);
				warehouseTimerDataPtr.updateTPMC(tpmc);
				warehouseTimerDataPtr.updateBTPS(btps);
				company.getTimerDataPtr(co).updateTPMC(tpmc);
				company.getTimerDataPtr(co).updateBTPS(btps);
				lock (company.stopThreadsCountMonitor) 
				{
					company.stopThreadsCount++;
					Monitor.Pulse(company.stopThreadsCountMonitor);
					//company.stopThreadsCountMonitor.notify();
				}
			}
		}//go

		public bool goValidate() 
		{
			Transaction[] t;
			t = new Transaction[maxTxnTypes];
			int i = 0;
			Transaction.validateRun(); // Start off assuming the run is valid.
			//for (i = 0; i < maxTxnTypes; ++i) 
			//{
			//	t[i] = Transaction.getInstance(Transaction.transactionClasses[i],
			//		company, warehouseId);
			//}

            foreach (Transaction.TransactionTypes txnType in Enum.GetValues(typeof(Transaction.TransactionTypes)))
            {
                t[i] = Transaction.GetTransactionInstance(txnType, company, warehouseId);
                i++;
            }


            for (i = 0; i < maxTxnTypes; ++i) 
			{
				t[i].init();
				t[i].initializeTransactionLog();
				t[i].process();
				t[i].processTransactionLog();
			}
			return Transaction.isRunValid();
		}

		public int[] buildDeck() 
		{
			int[] real_deck  = new int[33];
			int[] cross_deck = new int[33];
			int rand_val;
			int i;
			// set up cross_deck
			for (i = 0; i < 33; i++)
				cross_deck[i] = i;
			// assign new-order
			for (i = 0; i < 10; i++) 
			{
				rand_val = (int) JBButil.random(0, 33 - 1 - i, warehouseId);
				real_deck[cross_deck[rand_val]] = new_order;
				cross_deck[rand_val] = cross_deck[33 - 1 - i];
			}
			// assign payment
			for (i = 0; i < 10; i++) 
			{
				rand_val = (int) JBButil.random(0, 23 - 1 - i, warehouseId);
				real_deck[cross_deck[rand_val]] = payment;
				cross_deck[rand_val] = cross_deck[23 - 1 - i];
			}
			// order status
			rand_val = (int) JBButil.random(0, 13 - 1, warehouseId);
			real_deck[cross_deck[rand_val]] = order_status;
			cross_deck[rand_val] = cross_deck[13 - 1];
			// delivery
			rand_val = (int) JBButil.random(0, 12 - 1, warehouseId);
			real_deck[cross_deck[rand_val]] = delivery;
			cross_deck[rand_val] = cross_deck[12 - 1];
			// stock-level
			rand_val = (int) JBButil.random(0, 11 - 1, warehouseId);
			real_deck[cross_deck[rand_val]] = stock_level;
			cross_deck[rand_val] = cross_deck[11 - 1];
			// customer-report
			for (i = 0; i < 10; i++) 
			{
				rand_val = (int) JBButil.random(0, 10 - 1 - i, warehouseId);
				real_deck[cross_deck[rand_val]] = cust_report;
			}
			return real_deck;
		}


	}
}
