/*
 * 
 * 
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 * 
 * All rights reserved.
 * 
 * Copyright (c) 1996-2005 IBM Corporation, Inc. All rights reserved.
 * 
 */
using System;
using System.IO;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for TimerData.
	/// </summary>
	public class TimerData
	{
		// This goes right after each class/interface statement
		//  static readonly String          COPYRIGHT             = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private static sbyte          menu                  = 0;

		private static sbyte          typing                = 1;

		private static sbyte          thinking              = 2;

		private static sbyte          maxWaitTypes          = 3;

		private TransactionLogBuffer timerLog;

		// private int wait_times[][]; // time to wait (simulating user input
		// delays)
		private long[]                 wait_times_menu;                                                      // time

		// to wait (simulating user input delays)
		private long[]                 wait_times_typing;                                                    // time

		// to wait(simulating user input delays)
		private long[]                 wait_times_thinking;                                                  // time

		// to wait (simulating user input delays)
		private long[]                 transactionCount;                                                     // number

		// of transactions processed
		private long[]                 totalTime;                                                            // accumulated

		// transaction times
		private long[]                 minimumTime;                                                          // fastest

		// individual time
		private long[]                 maximumTime;                                                          // slowest

		// individual time
		private float[]                averageTime;                                                          // average

		// time per transaction
		private long[]                 totalTimeSquare;                                                      // accumulated

		// squares of transaction times
		private double[]               standardDeviationTime;

		private double[]               coefficientOfVariationTime;

		private long                   min_transaction_count = long.MaxValue;//Long.MAX_VALUE;

		private long                   max_transaction_count = long.MinValue;//Long.MIN_VALUE;

		private long                   rampup_time;                                                            // time

		// to run before measuring
		private long                   measurement_time;                                                       // time

		// to run while measuring
		private double                 tpmc;                                                                   // transactions

		// per minute final result for this object
		private double                 btps;                                                                   // "Business

		// Transaction per Second" final result/ for this object
		private short                  timer_flag;                                                             // timer

		// flag set to -number_of_terminals, is incremented as they checkin,
		// reaches zero to start rampup, changed to 1 to signal tracking
		private bool                useWaits;

		public TimerData()
		{
			int i;
			wait_times_menu = new long[Transaction.maxTxnTypes];
			wait_times_typing = new long[Transaction.maxTxnTypes];
			wait_times_thinking = new long[Transaction.maxTxnTypes];
			transactionCount = new long[Transaction.maxTxnTypes];
			totalTime = new long[Transaction.maxTxnTypes];
			minimumTime = new long[Transaction.maxTxnTypes];
			maximumTime = new long[Transaction.maxTxnTypes];
			averageTime = new float[Transaction.maxTxnTypes];
			totalTimeSquare = new long[Transaction.maxTxnTypes];
			standardDeviationTime = new double[Transaction.maxTxnTypes];
			coefficientOfVariationTime = new double[Transaction.maxTxnTypes];
			rampup_time = 0;
			measurement_time = 0;
			useWaits = false;
			tpmc = 0;
			btps = 0;
			for (i = 0; i < Transaction.maxTxnTypes; i++) 
			{
				wait_times_menu[i] = 0;
				wait_times_typing[i] = 0;
				wait_times_thinking[i] = 0;
				transactionCount[i] = 0;
				totalTime[i] = 0;
				minimumTime[i] = long.MaxValue;//Long.MAX_VALUE;
				maximumTime[i] = 0;
				averageTime[i] = 0;
				totalTimeSquare[i] = 0;
				standardDeviationTime[i] = 0.0;
				coefficientOfVariationTime[i] = 0.0;
			}
			min_transaction_count = long.MaxValue;//Long.MAX_VALUE;
			max_transaction_count = long.MaxValue;//Long.MIN_VALUE;
			buildDisplay();
		}

		public void initialize() 
		{
			int i;
			wait_times_menu = new long[Transaction.maxTxnTypes];
			wait_times_typing = new long[Transaction.maxTxnTypes];
			wait_times_thinking = new long[Transaction.maxTxnTypes];
			transactionCount = new long[Transaction.maxTxnTypes];
			totalTime = new long[Transaction.maxTxnTypes];
			minimumTime = new long[Transaction.maxTxnTypes];
			maximumTime = new long[Transaction.maxTxnTypes];
			averageTime = new float[Transaction.maxTxnTypes];
			totalTimeSquare = new long[Transaction.maxTxnTypes];
			standardDeviationTime = new double[Transaction.maxTxnTypes];
			coefficientOfVariationTime = new double[Transaction.maxTxnTypes];
			rampup_time = 0;
			measurement_time = 0;
			useWaits = false;
			tpmc = 0;
			btps = 0;
			for (i = 0; i < Transaction.maxTxnTypes; i++) 
			{
				wait_times_menu[i] = 0;
				wait_times_typing[i] = 0;
				wait_times_thinking[i] = 0;
				transactionCount[i] = 0;
				totalTime[i] = 0;
				minimumTime[i] = long.MaxValue;//Long.MAX_VALUE;
				maximumTime[i] = 0;
				averageTime[i] = 0;
				totalTimeSquare[i] = 0;
				standardDeviationTime[i] = 0.0;
				coefficientOfVariationTime[i] = 0.0;
			}
			min_transaction_count = long.MaxValue;//Long.MAX_VALUE;
			max_transaction_count = long.MinValue;//Long.MIN_VALUE;
			buildDisplay();
		}
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void zeroTimerData()
        {
            lock (_syncRoot)
            {
                tpmc = 0;
                btps = 0;
                for (int i = 0; i < Transaction.maxTxnTypes; i++)
                {
                    transactionCount[i] = 0;
                    totalTime[i] = 0;
                    minimumTime[i] = long.MaxValue;
                    maximumTime[i] = 0;
                    averageTime[i] = 0;
                    totalTimeSquare[i] = 0;
                    standardDeviationTime[i] = 0.0;
                    coefficientOfVariationTime[i] = 0.0;
                }
                min_transaction_count = long.MaxValue;//Long.MAX_VALUE;
                max_transaction_count = long.MinValue;//Long.MIN_VALUE;
                buildDisplay();
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void useWaitTimesPercentage(int percent)
        {
            lock (_syncRoot)
            {
                useWaits = true;
                // time is number of seconds
                wait_times_typing[Transaction.new_order] = (18000 * percent) / 100;
                wait_times_typing[Transaction.payment] = (3000 * percent) / 100;
                wait_times_typing[Transaction.order_status] = (2000 * percent) / 100;
                wait_times_typing[Transaction.delivery] = (2000 * percent) / 100;
                wait_times_typing[Transaction.stock_level] = (2000 * percent) / 100;
                wait_times_typing[Transaction.cust_report] = (2000 * percent) / 100;
                wait_times_thinking[Transaction.new_order] = (12000 * percent) / 100;
                wait_times_thinking[Transaction.payment] = (12000 * percent) / 100;
                wait_times_thinking[Transaction.order_status] = (10000 * percent) / 100;
                wait_times_thinking[Transaction.delivery] = (5000 * percent) / 100;
                wait_times_thinking[Transaction.stock_level] = (5000 * percent) / 100;
                wait_times_thinking[Transaction.cust_report] = (5000 * percent) / 100;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setRampUpTime(long in_rampup_time)
        {
            lock (_syncRoot)
            {   // convert seconds to milliseconds
                rampup_time = in_rampup_time;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setMeasurementTime(long in_measurement_time)
        {
            lock (_syncRoot)
            {   // convert seconds to milliseconds
                measurement_time = in_measurement_time;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getRampUpTime()
        {
            lock (_syncRoot)
            {
                long temp = rampup_time;
                return temp;
            }
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getMeasurementTime()
        {
            lock (_syncRoot)
            {
                long temp = measurement_time;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long[][] getWaitTimes()
        {
            lock (_syncRoot)
            {
                long[][] temp = new long[maxWaitTypes][];
                temp[menu] = wait_times_menu;
                temp[typing] = wait_times_typing;
                temp[thinking] = wait_times_thinking;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setWaitTimes(long[][] in_wait_times)
        {
            lock (_syncRoot)
            {
                int i;
                for (i = 0; i < Transaction.maxTxnTypes; i++)
                {
                    wait_times_menu[i] = in_wait_times[menu][i];
                    wait_times_typing[i] = in_wait_times[typing][i];
                    wait_times_thinking[i] = in_wait_times[thinking][i];
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getMenuWaitTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = wait_times_menu[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getTypingWaitTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = wait_times_typing[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getThinkingWaitTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = wait_times_thinking[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void updateTimerData(int transaction_type,
                                long transaction_time)
        {
            lock (_syncRoot)
            {
                ++transactionCount[transaction_type];
                totalTime[transaction_type] += transaction_time;
                totalTimeSquare[transaction_type] += transaction_time
                    * transaction_time;
                if (transaction_time < minimumTime[transaction_type])
                {
                    minimumTime[transaction_type] = transaction_time;
                }
                if (transaction_time > maximumTime[transaction_type])
                {
                    maximumTime[transaction_type] = transaction_time;
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void calculateResponseTimeStats()
        {
            lock (_syncRoot)
            {
                for (int i = 0; i < Transaction.maxTxnTypes; i++)
                {
                    if (transactionCount[i] != 0)
                    {
                        averageTime[i] = (float)totalTime[i]
                            / (float)transactionCount[i] / 1000f;
                        standardDeviationTime[i] = Math
                            .Sqrt(((double)transactionCount[i]
                            * (double)totalTimeSquare[i] - (double)totalTime[i]
                            * (double)totalTime[i])
                            / ((double)transactionCount[i] * ((double)transactionCount[i] - 1.0)));
                        coefficientOfVariationTime[i] = 100.0
                            * standardDeviationTime[i]
                            / ((double)totalTime[i] / (double)transactionCount[i]);
                    }
                    else
                    {
                        averageTime[i] = 0f;
                        standardDeviationTime[i] = 0.0;
                        coefficientOfVariationTime[i] = 0.0;
                    }
                }
            }
        }
        public long getTransactionCount(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = transactionCount[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getTotalTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = totalTime[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getTotalTimeSquare(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = totalTimeSquare[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getMinimumTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = minimumTime[transaction_type];
                return temp;
            }
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public long getMaximumTime(int transaction_type)
        {
            lock (_syncRoot)
            {
                long temp = maximumTime[transaction_type];
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void rollupTimerData(int transaction_type,
                                long transaction_count, long transaction_time,
                                long transaction_time_square, long minimum_transaction_time,
                                long maximum_transaction_time)
        {
            lock (_syncRoot)
            {
                transactionCount[transaction_type] += transaction_count;
                totalTime[transaction_type] += transaction_time;
                totalTimeSquare[transaction_type] += transaction_time_square;
                if (minimum_transaction_time < minimumTime[transaction_type]
                    || minimumTime[transaction_type] == 0)
                {
                    minimumTime[transaction_type] = minimum_transaction_time;
                }
                if (maximum_transaction_time > maximumTime[transaction_type])
                {
                    maximumTime[transaction_type] = maximum_transaction_time;
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void accumulateTransactionStats(long transaction_count)
        {
            lock (_syncRoot)
            {   // don't forget to zero out stuff
                if (transaction_count < min_transaction_count)
                {
                    min_transaction_count = transaction_count;
                }
                if (transaction_count > max_transaction_count)
                {
                    max_transaction_count = transaction_count;
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double getTPMC()
        {
            lock (_syncRoot)
            {
                double temp = tpmc;
                return temp;
            }
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double updateTPMC(double in_tpmc)
        {
            lock (_syncRoot)
            {
                tpmc += in_tpmc;
                return tpmc;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double updateTPMC(long elapsed_time)
        {
            lock (_syncRoot)
            {
                double temp;
                temp = (double)getTransactionCount(Transaction.new_order)
                    / ((double)elapsed_time / 1000d) * 60d;
                tpmc += temp;
                return tpmc;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double getBTPS()
        {
            lock (_syncRoot)
            {
                double temp = btps;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double updateBTPS(double in_btps)
        {
            lock (_syncRoot)
            {
                btps += in_btps;
                return btps;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public double updateBTPS(long elapsed_time)
        {
            lock (_syncRoot)
            {
                double temp;
                int transCount = 0;
                for (int i = 0; i < Transaction.maxTxnTypes; i++)
                {
                    transCount += (int)transactionCount[i];
                }
                temp = (double)transCount / ((double)elapsed_time / 1000f);
                btps += temp;
                return btps;
            }
        }
		private void buildDisplay() 
		{
			// " 1 2 3 4 5 6 7 "
			// "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
			// 0
			// "==============================================================================="
			// 1 "TOTALS FOR: COMPANY on Fri Mar 20 16:11:32 CST 1998"
			// 2 "========================= Results (time in seconds)
			// ========================"
			// 3 " Count Total Min Max Avg
			// 4 " New Order: 99999 99999.99 99.999 99.999 99.999
			// 5 " Payment: 99999 99999.99 99.999 99.999 99.999
			// 6 " Order Status: 99999 99999.99 99.999 99.999 99.999
			// 7 " Delivery: 99999 99999.99 99.999 99.999 99.999
			// 8 " Stock Level: 99999 99999.99 99.999 99.999 99.999
			// 9 " Cust Report : 99999 99999.99 99.999 99.999 99.999
			// 10
			// 11 " throughput = 999999.99 bops
			// "================================================================================
			// "
			//
			// WRR: The output line used to be:
			// 10 " result = 999999.99 "
			timerLog = new TransactionLogBuffer(12, 80);
			timerLog.putCharFill('=', 0, 0, 79);
			timerLog.putText("TOTALS FOR:", 0, 1, 12);
			int version_length = JBBmain.Version.Length;
			int text_length = 1 + version_length + 1 + 25 + 1;
			int fill_length = (79 - text_length) / 2;
			timerLog.putCharFill('.', 0, 2, fill_length);
			timerLog.putText(JBBmain.Version, fill_length + 1, 2, version_length);
			timerLog.putText("Results (time in seconds)", fill_length + 1
				+ version_length + 1, 2, 25);
			timerLog.putCharFill('.',
				fill_length + 1 + version_length + 1 + 25 + 1, 2, fill_length);
			timerLog.putText("Count", 19, 3, 5);
			timerLog.putText("Total", 29, 3, 5);
			timerLog.putText("Min", 38, 3, 3);
			timerLog.putText("Max", 46, 3, 3);
			timerLog.putText("Avg", 54, 3, 3);
			timerLog.putText("New Order:", 0, 4, 14,
				TransactionLogBuffer.alignRight);
			timerLog.putText("Payment:", 0, 5, 14, TransactionLogBuffer.alignRight);
			timerLog.putText("OrderStatus:", 0, 6, 14,
				TransactionLogBuffer.alignRight);
			timerLog
				.putText("Delivery:", 0, 7, 14, TransactionLogBuffer.alignRight);
			timerLog.putText("Stock Level:", 0, 8, 14,
				TransactionLogBuffer.alignRight);
			timerLog.putText("Cust Report:", 0, 9, 14,
				TransactionLogBuffer.alignRight);
			timerLog.putText("throughput =", 11, 11, 12);
			timerLog.putText("bops", 38, 11, 11);
			// timerLog.putCharFill('=', 0, 11, 79);
		}

		public void formatResults(TransactionLogBuffer timerLog) 
		{
			// " 1 2 3 4 5 6 7 "
			// "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
			// 0
			// "==============================================================================="
			// 1 "TOTALS FOR: COMPANY on Fri Mar 20 16:11:32 CST 1998"
			// 2 "========================= Results (time in seconds)
			// ========================"
			// 3 " Count Total Min Max Avg
			// 4 " New Order: 99999 99999.99 99.999 99.999 99.999
			// 5 " Payment: 99999 99999.99 99.999 99.999 99.999
			// 6 " Order Status: 99999 99999.99 99.999 99.999 99.999
			// 7 " Delivery: 99999 99999.99 99.999 99.999 99.999
			// 8 " Stock Level: 99999 99999.99 99.999 99.999 99.999
			// 9 " Cust Report 99999 99999.99 99.999 99.999 99.999
			// 10
			// 11 " throughput = 999999.99 "
			// "================================================================================
			// "
			//
			timerLog.putLong(transactionCount[Transaction.new_order], 16, 4, 8);
			timerLog.putDouble(totalTime[Transaction.new_order] / 1000.0d, 26, 4,
				8, 2);
			timerLog.putDouble(minimumTime[Transaction.new_order] / 1000.0d, 36, 4,
				6, 3);
			timerLog.putDouble(maximumTime[Transaction.new_order] / 1000.0d, 44, 4,
				6, 3);
			timerLog.putDouble(averageTime[Transaction.new_order], 52, 4, 6, 3);
			timerLog.putLong(transactionCount[Transaction.payment], 16, 5, 8);
			timerLog.putDouble(totalTime[Transaction.payment] / 1000.0d, 26, 5, 8,
				2);
			timerLog.putDouble(minimumTime[Transaction.payment] / 1000.0d, 36, 5,
				6, 3);
			timerLog.putDouble(maximumTime[Transaction.payment] / 1000.0d, 44, 5,
				6, 3);
			timerLog.putDouble(averageTime[Transaction.payment], 52, 5, 6, 3);
			timerLog.putLong(transactionCount[Transaction.order_status], 16, 6, 8);
			timerLog.putDouble(totalTime[Transaction.order_status] / 1000.0d, 26,
				6, 8, 2);
			timerLog.putDouble(minimumTime[Transaction.order_status] / 1000.0d, 36,
				6, 6, 3);
			timerLog.putDouble(maximumTime[Transaction.order_status] / 1000.0d, 44,
				6, 6, 3);
			timerLog.putDouble(averageTime[Transaction.order_status], 52, 6, 6, 3);
			timerLog.putLong(transactionCount[Transaction.delivery], 16, 7, 8);
			timerLog.putDouble(totalTime[Transaction.delivery] / 1000.0d, 26, 7, 8,
				2);
			timerLog.putDouble(minimumTime[Transaction.delivery] / 1000.0d, 36, 7,
				6, 3);
			timerLog.putDouble(maximumTime[Transaction.delivery] / 1000.0d, 44, 7,
				6, 3);
			timerLog.putDouble(averageTime[Transaction.delivery], 52, 7, 6, 3);
			timerLog.putLong(transactionCount[Transaction.stock_level], 16, 8, 8);
			timerLog.putDouble(totalTime[Transaction.stock_level] / 1000.0d, 26, 8,
				8, 2);
			timerLog.putDouble(minimumTime[Transaction.stock_level] / 1000.0d, 36,
				8, 6, 3);
			timerLog.putDouble(maximumTime[Transaction.stock_level] / 1000.0d, 44,
				8, 6, 3);
			timerLog.putDouble(averageTime[Transaction.stock_level], 52, 8, 6, 3);
			timerLog.putLong(transactionCount[Transaction.cust_report], 16, 9, 8);
			timerLog.putDouble(totalTime[Transaction.cust_report] / 1000.0d, 26, 9,
				8, 2);
			timerLog.putDouble(minimumTime[Transaction.cust_report] / 1000.0d, 36,
				9, 6, 3);
			timerLog.putDouble(maximumTime[Transaction.cust_report] / 1000.0d, 44,
				9, 6, 3);
			timerLog.putDouble(averageTime[Transaction.cust_report], 52, 9, 6, 3);
			// WRR: Eliminate tpmc report.
			// timerScreen.putDouble(tpmc, 25, 10, 9, 2);
			// timerScreen.putDouble(btps, 43, 10, 9, 2);
			timerLog.putDouble(btps, 25, 11, 12, 2);
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void displayResults(String title, long totalMem,
                                long freeMem)
        {
            lock (_syncRoot)
            {   // " 1 2 3 4 5 6 7 "
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // 0
                // "==============================================================================="
                // 1 "TOTALS FOR: COMPANY on Fri Mar 20 16:11:32 CST 1998"
                // 2 "========================= Results (time in seconds)
                // ========================"
                // 3 " Count Total Min Max Avg Heap Space
                // 4 " New Order: 99999 99999.99 99.999 99.999 99.999 total 99999.9MB
                // 5 " Payment: 99999 99999.99 99.999 99.999 99.999 used 99999.9MB
                // 6 " Order Status: 99999 99999.99 99.999 99.999 99.999
                // 7 " Delivery: 99999 99999.99 99.999 99.999 99.999
                // 8 " Stock Level: 99999 99999.99 99.999 99.999 99.999
                // 9 " Cust Report: 99999 9999.99 99.999 99.999 99.999
                // 10
                // 11 " throughput = 999999999.99 bops
                // "==============================================================================="
                // timerScreen.copyScreen(timerScreen);
                long usedMem = totalMem - freeMem;
                double totalMB = ((double)totalMem) / 1048576.0D;
                double usedMB = ((double)usedMem) / 1048576.0D;
                timerLog.putText(title, 12, 1, 60);
                formatResults(timerLog);
                timerLog.putText("Heap Space", 61, 3, 10);
                timerLog.putDouble(totalMB, 66, 4, 11, 1);
                timerLog.putText("total", 61, 4, 5);
                timerLog.putText("MB", 77, 4, 2);
                timerLog.putDouble(usedMB, 66, 5, 11, 1);
                timerLog.putText(" used", 61, 5, 5);
                timerLog.putText("MB", 77, 5, 2);
                // print out information
                timerLog.display(System.Console.Out);
                using (Stream stream = File.Open(@"results/results.txt", FileMode.Append, FileAccess.Write, FileShare.Read))
                using (TextWriter writer = new StreamWriter(stream))
                {
                    timerLog.display(writer);
                }
                // need to put these into one buffer for the multiuser version
            }
        }
		public void propResults(String prefix, StreamWriter out1) 
		{
			// This might be a problem is the locks were really implemented. The
			// lock
			// would need to be recursive.
			out1.WriteLine(prefix + "new_order.count="
			+ transactionCount[Transaction.new_order]);
			out1.WriteLine(prefix + "new_order.totaltime="
			+ totalTime[Transaction.new_order] / 1000.0d);
			out1.WriteLine(prefix + "new_order.minimumtime="
			+ minimumTime[Transaction.new_order] / 1000.0d);
			out1.WriteLine(prefix + "new_order.maximumtime="
			+ maximumTime[Transaction.new_order] / 1000.0d);
			out1.WriteLine(prefix + "new_order.averagetime="
			+ averageTime[Transaction.new_order]);
			out1.WriteLine(prefix + "new_order.stddevtime="
			+ standardDeviationTime[Transaction.new_order] / 1000.0d);
			out1.WriteLine(prefix + "new_order.coeffvartime="
			+ coefficientOfVariationTime[Transaction.new_order]);
			out1.WriteLine(prefix + "payment.count="
			+ transactionCount[Transaction.payment]);
			out1.WriteLine(prefix + "payment.totaltime="
			+ totalTime[Transaction.payment] / 1000.0d);
			out1.WriteLine(prefix + "payment.minimumtime="
			+ minimumTime[Transaction.payment] / 1000.0d);
			out1.WriteLine(prefix + "payment.maximumtime="
			+ maximumTime[Transaction.payment] / 1000.0d);
			out1.WriteLine(prefix + "payment.averagetime="
			+ averageTime[Transaction.payment]);
			out1.WriteLine(prefix + "payment.stddevtime="
			+ standardDeviationTime[Transaction.payment] / 1000.0d);
			out1.WriteLine(prefix + "payment.coeffvartime="
			+ coefficientOfVariationTime[Transaction.payment]);
			out1.WriteLine(prefix + "order_status.count="
			+ transactionCount[Transaction.order_status]);
			out1.WriteLine(prefix + "order_status.totaltime="
			+ totalTime[Transaction.order_status] / 1000.0d);
			out1.WriteLine(prefix + "order_status.minimumtime="
			+ minimumTime[Transaction.order_status] / 1000.0d);
			out1.WriteLine(prefix + "order_status.maximumtime="
			+ maximumTime[Transaction.order_status] / 1000.0d);
			out1.WriteLine(prefix + "order_status.averagetime="
			+ averageTime[Transaction.order_status]);
			out1.WriteLine(prefix + "order_status.stddevtime="
			+ standardDeviationTime[Transaction.order_status] / 1000.0d);
			out1.WriteLine(prefix + "order_status.coeffvartime="
			+ coefficientOfVariationTime[Transaction.order_status]);
			out1.WriteLine(prefix + "delivery.count="
			+ transactionCount[Transaction.delivery]);
			out1.WriteLine(prefix + "delivery.totaltime="
			+ totalTime[Transaction.delivery] / 1000.0d);
			out1.WriteLine(prefix + "delivery.minimumtime="
			+ minimumTime[Transaction.delivery] / 1000.0d);
			out1.WriteLine(prefix + "delivery.maximumtime="
			+ maximumTime[Transaction.delivery] / 1000.0d);
			out1.WriteLine(prefix + "delivery.averagetime="
			+ averageTime[Transaction.delivery]);
			out1.WriteLine(prefix + "delivery.stddevtime="
			+ standardDeviationTime[Transaction.delivery] / 1000.0d);
			out1.WriteLine(prefix + "delivery.coeffvartime="
			+ coefficientOfVariationTime[Transaction.delivery]);
			out1.WriteLine(prefix + "stock_level.count="
			+ transactionCount[Transaction.stock_level]);
			out1.WriteLine(prefix + "stock_level.totaltime="
			+ totalTime[Transaction.stock_level] / 1000.0d);
			out1.WriteLine(prefix + "stock_level.minimumtime="
			+ minimumTime[Transaction.stock_level] / 1000.0d);
			out1.WriteLine(prefix + "stock_level.maximumtime="
			+ maximumTime[Transaction.stock_level] / 1000.0d);
			out1.WriteLine(prefix + "stock_level.averagetime="
			+ averageTime[Transaction.stock_level]);
			out1.WriteLine(prefix + "stock_level.stddevtime="
			+ standardDeviationTime[Transaction.stock_level] / 1000.0d);
			out1.WriteLine(prefix + "stock_level.coeffvartime="
			+ coefficientOfVariationTime[Transaction.stock_level]);
			out1.WriteLine(prefix + "cust_report.count="
			+ transactionCount[Transaction.cust_report]);
			out1.WriteLine(prefix + "cust_report.totaltime="
			+ totalTime[Transaction.cust_report] / 1000.0d);
			out1.WriteLine(prefix + "cust_report.minimumtime="
			+ minimumTime[Transaction.cust_report] / 1000.0d);
			out1.WriteLine(prefix + "cust_report.maximumtime="
			+ maximumTime[Transaction.cust_report] / 1000.0d);
			out1.WriteLine(prefix + "cust_report.averagetime="
			+ averageTime[Transaction.cust_report]);
			out1.WriteLine(prefix + "cust_report.stddevtime="
			+ standardDeviationTime[Transaction.cust_report] / 1000.0d);
			out1.WriteLine(prefix + "cust_report.coeffvartime="
			+ coefficientOfVariationTime[Transaction.cust_report]);
			out1.WriteLine(prefix + "score=" + btps);
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void propResults(String prefix, StreamWriter out1,
        long totalMem, long freeMem)
        {
            lock (_syncRoot)
            {
                propResults(prefix, out1);
                long usedMem = totalMem - freeMem;
                double totalMB = ((double)totalMem) / 1048576.0D;
                double usedMB = ((double)usedMem) / 1048576.0D;
                out1.WriteLine(prefix + "heapsize=" + totalMB);
                out1.WriteLine(prefix + "heapused=" + usedMB);
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void displayThreadResults()
        {
            lock (_syncRoot)
            {
                long diff = max_transaction_count - min_transaction_count;
                float diff_pct = 100 * (float)diff / (float)min_transaction_count;
                // System.out.println("");
                // System.out.println("Minimum transactions by a thread = " +
                // min_transaction_count);
                // System.out.println("Maximum transactions by a thread = " +
                // max_transaction_count);
                // System.out.println("Difference = " + diff + " (" + diff_pct + "%)");
                // System.out.println("");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void propThreadResults(String prefix, StreamWriter out1)
        {
            lock (_syncRoot)
            {
                out1.WriteLine(prefix + "min_thread_transactions="
            + min_transaction_count);
                out1.WriteLine(prefix + "max_thread_transactions="
                + max_transaction_count);
            }
        }
	}
}
