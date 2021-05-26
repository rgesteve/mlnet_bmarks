/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 * 
 */
using System;
using System.IO;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;
using Specjbb2005.src.spec.jbb.infra.Util;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for DeliveryTransaction.
	/// </summary>
	public class DeliveryTransaction : Transaction
	{
		// This goes right after each class/interface statement
		//  internal static readonly String          COPYRIGHT     = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Company              company;

		private short                warehouseId;

		private short                carrierId;

		private DeliveryHandler      deliveryHandler;

		private Warehouse            warehousePtr;

		private long                 deliveryTime;

		private TransactionLogBuffer deliveryLog;

		private TransactionLogBuffer initLog;

		private TransactionLogBuffer queuedLog;

		private XMLTransactionLog    xmlDeliveryLog;

		String[]                     validationLog = {
														 "                                     Delivery", "Warehouse:    1",
														 "", "Carrier Number:  3", "",
														 "Execution Status: Delivery has been queued ", "", "", "", "", "",
														 "", "", "", "", "", "", "", "", "", "", "", "", ""
													 };

		bool[]                    checkLine     = {
														 true, true, true, true, true, true, true, true, true, true, true,
														 true, true, true, true, true, true, true, true, true, true, true,
														 true, true
													 };
        private readonly object _syncRoot = new Object(); // CORECLR

        public DeliveryTransaction(Company aCompany, short aWarehouse) 
		{
			company = aCompany;
			warehouseId = aWarehouse;
			warehousePtr = company.getWarehousePtr(warehouseId, false);
			deliveryHandler = new DeliveryHandler(aCompany.getOutDeliveriesFile());
			deliveryLog = new TransactionLogBuffer();
			setupDeliveryLog();
			xmlDeliveryLog = new XMLTransactionLog();
			initLog = new TransactionLogBuffer(deliveryLog);
			setupInitLog();
			queuedLog = new TransactionLogBuffer(16, 64);
		}

		private void setupDeliveryLog() 
		{
			deliveryLog.putText("Delivery", 37, 0, 8);
			deliveryLog.putText("Warehouse:", 0, 1, 10);
			deliveryLog.putInt(warehouseId, 11, 1, 4);
			deliveryLog.putText("Carrier Number:", 0, 3, 15);
			deliveryLog.putText("Execution Status:", 0, 5, 17);
		}

		private void setupInitLog() 
		{
			initLog.putCharFill('9', 16, 3, 2);
			initLog.putCharFill('X', 18, 5, 25);
		}

		public override String getMenuName() 
		{
			return "Delivery";
		}

		public void delete() 
		{
		}

		public override void init() 
		{
			//JBButil.getLog().entering("spec.jbb.DeliveryTransaction", "init");
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.DeliveryTransaction::init");
			carrierId = (short) JBButil.random(1, 10, warehouseId); // 2.7.1.2
			//JBButil.getLog().exiting("spec.jbb.DeliveryTransaction", "init");
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Exiting spec.jbb.DeliveryTransaction::init");
		}

		public bool preprocess() 
		{
			// place warehouse ID in output stream
			queuedLog.putText("Warehouse:", 0, 1, 10);
			queuedLog.putInt(warehouseId, 11, 1, 4);
			// place carrier ID in output stream
			queuedLog.putText("Carrier ID:", 16, 1, 12);
			queuedLog.putInt(carrierId, 28, 1, 2);
			// place header for order info in output stream
			queuedLog.putText("Items Delivered", 0, 2, 15);
			queuedLog.putText("District            Order", 0, 3, 25);
			int distCount = warehousePtr.getDistrictCount();
			int distId;
			// iterate over the district list of the given warehouse
			District currentDistrict;
			DateTime delDate = DateTime.Now ;//new Date();
			for (distId = 1; distId <= distCount; distId++) 
			{
				currentDistrict = warehousePtr.getDistrict(distId);
				JBBDataStorage newOrderTable = currentDistrict.getNewOrderTable();
                        //09-04-07 Li: Need lock newOrderTable which is the underneath collection
                        //of enumeration for possibleOrderIter.
                        lock (newOrderTable)
                        {
				IEnumerator possibleOrderIter = newOrderTable.elements(); 
				//Iterator possibleOrderIter = currentDistrict.newOrderIter();
				NewOrder possibleNewOrder = null;
				Order possibleOrder = null;
				Customer possibleCustomerPtr = null;
				Orderline[] requiredOrderLine;
				Orderline orderline;
				while (possibleOrderIter.MoveNext())//.hasNext()) 
				{
					possibleNewOrder = (NewOrder) possibleOrderIter.Current ;//.next();
					possibleOrder = possibleNewOrder.getOrderPtr();
					possibleCustomerPtr = possibleOrder.getCustomerPtr();
					// check if customer balance is greater than order amount
					Decimal hisBalance = possibleCustomerPtr.getBalance();
					Decimal orderAmount = possibleOrder.getTotalAmount();
					if ((hisBalance.CompareTo(orderAmount)) == -1) 
					{
						continue;
					}
					// check if the ordered quantity is available in stock at that
					// moment
					requiredOrderLine = possibleOrder.getOrderlineList();
					for (int i = 0; i < requiredOrderLine.Length; i++) 
					{
						orderline = requiredOrderLine[i];
						if (orderline == null) 
						{
							continue;
						}
                        
                        int requiredQuantity = orderline.getQuantity();
                        //short requiredQuantity = orderline.getQuantity();
                        int itemId = orderline.getItemId();
                        Stock stock = warehousePtr.retrieveStock(itemId);
                        int availableQuantity = stock.getQuantity();
                        if (availableQuantity >= requiredQuantity)
                        {
                            stock.changeQuantity(-requiredQuantity);
                            break;
                        }
                        
					}
				}

				if (possibleNewOrder != null) 
				{
					NewOrder currentNewOrder = currentDistrict.removeNewOrder(possibleNewOrder.getId());
					if (Transaction.steadyStateMem) 
					{
						currentDistrict.removeOldNewOrders((company.getInitialOrders() - company.getInitialNewOrders()));
						currentDistrict.removeOldOrders(company.getInitialOrders());
					}
					Order matchingOrder = currentNewOrder.getOrderPtr(); 
					matchingOrder.setCarrierId(carrierId);
					matchingOrder.dateOrderlines(delDate);//ref for structs.
					queuedLog.putInt(currentDistrict.getId(), 6, distId + 3, 2);
					queuedLog.putInt(currentDistrict.getId(), 20, distId + 3, 5);
					// get customer ptr with a write lock
					Customer customerPtr = matchingOrder.getCustomerPtr();
					customerPtr.adjustBalance(matchingOrder.getTotalAmount());
					customerPtr.incrementDeliveryCount();
					// commit
			        } // if
                        }//lock
			} // if
			// place finish time in output stream
			DateTime dayTime = DateTime.Now;//new Date();
			queuedLog.putText("Processing finished at:", 0, 14, 23);
			queuedLog.putDate(ref dayTime, 24, 14, 10);
			queuedLog.putTime(ref dayTime, 36, 14, 10);
			return true;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display(StreamWriter out1)
        {
            lock (_syncRoot)
            {
                // " 1 2 3 4 5 6 7 "
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // "********************************************************************************"
                // 00 "Queueing Time:Wed Jan 15 13:07:05 CST 1997"
                // 01 "Warehouse: 9999 Carrier ID: 99"
                // 02 "Items Delivered "
                // 03 "District Order"
                // 04 " 99 99999"
                // 05 " 99 99999"
                // 06 " 99 99999"
                // 07 " 99 99999"
                // 08 " 99 99999"
                // 09 " 99 99999"
                // 10 " 99 99999"
                // 11 " 99 99999"
                // 12 " 99 99999"
                // 13 " 99 99999"
                // 14 "Processing finished at: Wed Jan 15 13:07:05 CST 1997"
                // 15 ""
                // "********************************************************************************"
                lock (out1)
                {
                    if (Transaction.enableLogWrite)
                        queuedLog.display(out1);
                }
            }
        }

		public override void initializeTransactionLog() 
		{
			//JBButil.getLog().entering("spec.jbb.DeliveryTransaction","initializeTransactionLog");
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.DeliveryTransaction::initializeTransactionLog");
			// " 1 2 3 4 5 6 7 "
			// "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
			// "********************************************************************************"
			// " DELIVERY"
			// "Warehouse: 9999"
			// ""
			// "Carrier Number: 99"
			// ""
			// "Execution Status: XXXXXXXXXXXXXXXXXXXXXXXXX"
			// "\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n"
			// "********************************************************************************"
			if (Transaction.enableLogWrite)
			initLog.display();
			//JBButil.getLog().exiting("spec.jbb.DeliveryTransaction","initializeTransactionLog");
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Exiting spec.jbb.DeliveryTransaction::initializeTransactionLog");
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void processTransactionLog()
        {
            lock (_syncRoot)
            {
                //JBButil.getLog().entering("spec.jbb.DeliveryTransaction","processTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.DeliveryTransaction::processTransactionLog");
                deliveryLog.clearBuffer();
                xmlDeliveryLog.clear();
                setupDeliveryLog();
                // " 1 2 3 4 5 6 7 "
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // "********************************************************************************"
                // " DELIVERY"
                // "Warehouse: 9999"
                // deliveryScreen.putInt (warehouseId, 11, 1, 4); // already done by
                // ctor SJM
                // ""
                // "Carrier Number: 99"
                deliveryLog.putInt(carrierId, 16, 3, 2);
                // ""
                // "Execution Status: XXXXXXXXXXXXXXXXXXXXXXXXX"
                deliveryLog.putText("Delivery has been queued", 18, 5, 25);
                // "\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n"
                // "********************************************************************************"
                // create XML representation
                xmlDeliveryLog.populateXML(deliveryLog);
                if (Transaction.enableLogWrite)
                    deliveryLog.display();
                if (Transaction.validationFlag)
                {
                    String[] s = deliveryLog.validate();
                    if (s.Length != validationLog.Length)
                    {
                        StringBuilder sb = new StringBuilder(200);
                        sb.Append("VALIDATION ERROR:  mismatch in screen lengths for DeliveryTransaction");
                        sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                        sb.Append("    Screen length should be:  "
                            + validationLog.Length);
                        sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                        sb.Append("    Screen length is:  " + s.Length);
                        Trace.WriteLineIf(JBButil.getLog().TraceWarning, sb.ToString());//JBButil.getLog().warning(sb.ToString());
                        Transaction.invalidateRun();
                    }
                    for (int i = 0; i < validationLog.Length; i++)
                    {
                        if (checkLine[i])
                        {
                            if (!s[i].Equals(validationLog[i]))
                            {
                                StringBuilder sb = new StringBuilder(200);
                                sb.Append("VALIDATION ERROR:  incorrect output for DeliveryTransaction");
                                sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                                sb.Append("    Line " + (i + 1) + " should be:  |"
                                    + validationLog[i] + "|");
                                sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                                sb.Append("    Line " + (i + 1) + " is:  |" + s[i]
                                    + "|");
                                Trace.WriteLineIf(JBButil.getLog().TraceWarning, sb.ToString());//JBButil.getLog().warning(sb.ToString());
                                Transaction.invalidateRun();
                            }
                        }
                    }
                }
                //JBButil.getLog().exiting("spec.jbb.DeliveryTransaction","processTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Exiting spec.jbb.DeliveryTransaction::processTransactionLog");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override bool process()
        {
            lock (_syncRoot)
            {
                //JBButil.getLog().entering("spec.jbb.DeliveryTransaction", "process");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.DeliveryTransaction::process");
                DateTime dayTime = DateTime.Now;//new Date();
                                                // place queuing time in output stream
                queuedLog.clearBuffer();
                queuedLog.putText("Queueing Time:", 0, 0, 12);
                queuedLog.putDate(ref dayTime, 12, 0, 10);
                queuedLog.putTime(ref dayTime, 24, 0, 10);
                // place in deliveryHandler's queue
                deliveryHandler.handleDelivery(this);
                //JBButil.getLog().exiting("spec.jbb.DeliveryTransaction", "process");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Exiting spec.jbb.DeliveryTransaction::process");
                return true;
            }
        }
	}//public class
} //namespace
