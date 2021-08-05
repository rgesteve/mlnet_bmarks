/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */

using System;
using System.Collections;
using System.Diagnostics;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for CustomerReportTransaction.
	/// </summary>
	public class CustomerReportTransaction : Transaction
	{
		// This goes right after each class/interface statement
		//  static readonly String          COPYRIGHT            = "SPECjbb2005,"
		//  	+ "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  	+ "All rights reserved,"
		//  	+ "(C) Copyright IBM Corp., 1996 - 2005"
		//  	+ "All rights reserved,"
		//  	+ "Licensed Materials - Property of SPEC";

		private Company              company;

		private short                warehouseId;

		private sbyte                 districtId;

		private bool                 home;

		private char                 use_customerId;

		private String               cust_last_name;

		private short                customerId;

		private short                customerWarehouseId;

		private sbyte                customerDistrictId;

		private DateTime             reportTime;

		private Warehouse            warehousePtr;

		private District             districtPtr;

		private Customer             customerPtr;

		private TransactionLogBuffer initLog;

		private TransactionLogBuffer customerLog;

		private XMLTransactionLog    xmlCustomerLog;

		int                          lastPaymentsQuantity = 5;

		int                          lastOrdersQuantity   = 5;

		DateTime[]                   paymentDates        ;// = new DateTime[lastPaymentsQuantity];

		Decimal[]                   paymentAmount         ;//= new Decimal[lastPaymentsQuantity];

		DateTime[]                   orderDates           ;//= new DateTime[lastOrdersQuantity];

		Decimal[]                    orderAmount          ;//= new Decimal[lastOrdersQuantity];

		String[]                     validationScreen     = 
													{
														"                                 CUSTOMER REPORT",
														"Date: 31-12-2004 04:30:07",
														"",
														"Warehouse:    1                          District:  1",
														"mypIw7RUtW7hoQdmASd                      Q5pck00LNkV         ",
														"vIfg0HYwMq                               U2KXH6WkfD          ",
														"PWBAjD5CvVrZNaS      Vv 76447-9863       8FUvKJooC6SiWztmS    Wu 76407-6439",
														"",
														"Customer:   19  Cust-Warehouse:    1  Cust-District:  1",
														"Name:   5C8n4mfBkU32Y8   OE BARABLEPRES          Since:  31-12-2004",
														"        Dy6cn4FXooQQHqDkl                        Credit: GC",
														"        syXdFH3PQeJjwyR6Kr                       %Disc:   2.65",
														"Last payments         Date:                   Amount:",
														"                      31-12-2004 04:30:07    $10.00",
														"                      31-12-2004 04:30:07    $10.00",
														"                      31-12-2004 04:30:07    $10.00",
														"                      31-12-2004 04:30:07    $10.00",
														"                      31-12-2004 04:30:07    $10.00",
														"Last orders           Date:                   Amount:",
														"                      31-12-2004 04:30:07    $3594.00", "", "",
														"", ""
													};

		bool   []                    checkLine            = {
																true, false, true, true, true, true, true, true, true, false, true,
																true, true, false, false, false, false, false, true, false, true,
																true, true, true
															};

        private readonly object _syncRoot = new Object(); // CORECLR

        /*
		static CustomerReportTransaction() 
		{
			//Moved from the class. check this out
			DateTime[]    paymentDates         = new DateTime[CustomerReportTransaction.lastPaymentsQuantity];
			Decimal[]     paymentAmount        = new Decimal[CustomerReportTransaction.lastPaymentsQuantity];
			DateTime[]    orderDates           = new DateTime[CustomerReportTransaction.lastOrdersQuantity];
			Decimal[]       orderAmount        = new Decimal[CustomerReportTransaction.lastOrdersQuantity];
		}*/

        public CustomerReportTransaction(Company inCompany, short inWarehouseId)
		{
			paymentDates         = new DateTime[this.lastPaymentsQuantity];
			paymentAmount        = new Decimal[this.lastPaymentsQuantity];
			orderDates           = new DateTime[this.lastOrdersQuantity];
			orderAmount          = new Decimal[this.lastOrdersQuantity];

			company = inCompany;
			warehouseId = inWarehouseId;
			warehousePtr = company.getWarehousePtr(warehouseId, false);
			customerLog = new TransactionLogBuffer();
			setupCustomerLog();
			xmlCustomerLog = new XMLTransactionLog();
			initLog = new TransactionLogBuffer(customerLog);
			setupInitLog();
		}

		private void setupCustomerLog() 
		{
			customerLog.putText("CUSTOMER REPORT", 33, 0, 15);
			customerLog.putText("Date:", 0, 1, 5);
			customerLog.putText("Warehouse:", 0, 3, 10);
			customerLog.putText("District:", 41, 3, 9);
			customerLog.putText("Customer:", 0, 8, 9);
			customerLog.putText("Cust-Warehouse:", 16, 8, 15);
			customerLog.putText("Cust-District:", 38, 8, 14);
			customerLog.putText("Name:", 0, 9, 5);
			customerLog.putText("Since:", 49, 9, 6);
			customerLog.putText("Credit:", 49, 10, 7);
			customerLog.putText("%Disc:", 49, 11, 6);
			customerLog.putText("Last payments", 0, 12, 13);
			customerLog.putText("Date:", 22, 12, 5);
			customerLog.putText("Amount:", 46, 12, 7);
			customerLog.putText("Last orders", 0, 18, 11);
			customerLog.putText("Date:", 22, 18, 5);
			customerLog.putText("Amount:", 46, 18, 7);
		}

		private void setupInitLog() 
		{
			int i;
			initLog.putText("DD-MM-YYYY", 6, 1, 10);
			initLog.putText("hh:mm:ss", 17, 1, 8);
			initLog.putCharFill('9', 11, 3, 4);
			initLog.putCharFill('9', 51, 3, 2);
			initLog.putCharFill('X', 0, 4, 20);
			initLog.putCharFill('X', 41, 4, 20);
			initLog.putCharFill('X', 0, 5, 20);
			initLog.putCharFill('X', 41, 5, 20);
			initLog.putCharFill('X', 0, 6, 20);
			initLog.putCharFill('X', 21, 6, 2);
			initLog.putText("XXXXX-XXXX", 24, 6, 10);
			initLog.putCharFill('X', 41, 6, 20);
			initLog.putCharFill('X', 62, 6, 2);
			initLog.putText("XXXXX-XXXX", 65, 6, 10);
			initLog.putCharFill('9', 10, 8, 4);
			initLog.putCharFill('9', 32, 8, 4);
			initLog.putCharFill('9', 53, 8, 2);
			initLog.putCharFill('X', 8, 9, 16);
			initLog.putCharFill('X', 25, 9, 2);
			initLog.putCharFill('X', 28, 9, 16);
			initLog.putText("DD-MM-YYYY", 57, 9, 10);
			initLog.putCharFill('X', 8, 10, 20);
			initLog.putCharFill('X', 57, 10, 2);
			initLog.putCharFill('X', 8, 11, 20);
			initLog.putText("99.99", 57, 11, 5);
			for (i = 0; i < 5; i++) 
			{
				initLog.putText("DD-MM-YYYY", 22, 13 + i, 10);
				initLog.putText("hh:mm:ss", 33, 13 + i, 8);
				initLog.putText("$9999.99", 45, 13 + i, 8);
			}
			for (i = 0; i < 5; i++) 
			{
				initLog.putText("DD-MM-YYYY", 22, 19 + i, 10);
				initLog.putText("hh:mm:ss", 33, 19 + i, 8);
				initLog.putText("$9999.99", 45, 19 + i, 8);
			}
		}

		public override String getMenuName() 
		{
			return "CustomerReport";
		}
		
		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public void delete() 
		{
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void init()
        {
            lock (_syncRoot)
            {
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        "Entering spec.jbb.CustomerReportTransaction::init");
                //JBButil.getLog().entering("spec.jbb.CustomerReportTransaction", "init");
                districtId = (sbyte)JBButil.random(1, company
                    .getMaxDistrictsPerWarehouse(), warehouseId);
                districtPtr = warehousePtr.getDistrictPtr(districtId, false);
                int x = (int)JBButil.random(1, 100, warehouseId);
                int y = (int)JBButil.random(1, 100, warehouseId);
                if (y <= 60)
                {
                    cust_last_name = JBButil.choose_random_last_name(company
                        .getMaxCustomersPerDistrict(), warehouseId);
                    use_customerId = 'F';
                }
                else
                {
                    customerId = JBButil.create_random_customer_id(company
                        .getMaxCustomersPerDistrict(), warehouseId);
                    use_customerId = 'T';
                }
                if (x <= 85)
                {
                    customerDistrictId = districtId;
                    customerWarehouseId = warehouseId;
                }
                else
                {
                    customerDistrictId = (sbyte)JBButil.random(1, company
                        .getMaxDistrictsPerWarehouse(), warehouseId);
                    int maxWarehouses = company.getMaxWarehouses();
                    for (customerWarehouseId = (short)JBButil.random(1, maxWarehouses,
                        warehouseId); (customerWarehouseId == warehouseId)
                        && (maxWarehouses > 1); customerWarehouseId = (short)JBButil
                        .random(1, maxWarehouses, warehouseId))
                        ;
                }
                home = (customerWarehouseId == warehouseId);
                //JBButil.getLog().exiting("spec.jbb.CustomerReportTransaction", "init");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "Exiting spec.jbb.CustomerReportTransaction::init");
            }
        }
		public override bool process() 
		{
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, 
				"Entering spec.jbb.CustomerReportTransaction::process");
			String warehousename = warehousePtr.getName();
			switch (use_customerId) 
			{
				case 'T': 
				{
					long uniqueCustomerId = company.buildUniqueCustomerKey(
						customerWarehouseId, customerDistrictId, customerId);
					// get customer ptr with write lock
					customerPtr = company.getCustomer(uniqueCustomerId, true);
				}
					break;
				case 'F':
					// get customer ptr with write lock
					customerPtr = company
						.getCustomerByLastName(customerWarehouseId,
						customerDistrictId, cust_last_name);
					break;
			}
			if (customerPtr == null) 
			{
			}
			else 
			{
				JBBDataStorage historyTable = warehousePtr.getHistoryTable();
				History history;
                                History[] payments;
				int histCount = 0; 
				int i = 0;

                        //09-04-07 Li: To avoid IEnumberator throws exception caused by underneath collection was 
                        //modified, we need to lock the collection which is the historyTable here for enumeration.
                        lock (historyTable)
                        {
				            //Iterator historyIter = historyTable.elements();
				            IEnumerator historyIter = historyTable.elements();
                                    //09-04-07 Li: On heavy testing, it can throw IndexOutOfBoundsException on payments
                                    //for example, if the table has a,b,c 3 elements, according to algorithm below, 
                                    //histCount++, payments[histCount] = history will be payments[1]=a, payment[2]=b,
                                    //payment[3]=c, payment[3] needs a array size euqual = 4, which is talbe size(3)+1. 
				    payments = new History[historyTable.size()+1];
				
                                    //NOTE: Some times, I get an exception here InvalidOperationException
                                    //telling me that underlying collection was modified. IEnumerator 
                                    //interface throws exception when we call movenext if the underlying
                                    //collection was modified.
                                    while (historyIter.MoveNext())//(historyIter.hasNext()) 
				            {
					            history = (History) historyIter.Current;//historyIter.next();
					            if (history.getCustomerId() == customerPtr.getId()) 
					            {
						             histCount++;
						             payments[histCount] = history;
					            }
				            }            
                        }//lock
				for (i = histCount; i > 0; i--) 
				{
					if (histCount - i < lastPaymentsQuantity) 
					{
						paymentDates[histCount - i] = payments[i].getDate();
						paymentAmount[histCount - i] = payments[i].getAmount();
					}
					else 
					{
						break;
					}
				}
				JBBDataStorage orderTable = districtPtr.getOrderTable();
				Order order;
				Order[] lastOrders;
				int orderCount = 0;
				int lastOrdersQuantity = 5;
                        //9-04-07 Li: lock underneath collection for enumberation
                        lock (orderTable)
                        {
				      IEnumerator orderIter = orderTable.elements();
				      lastOrders = new Order[orderTable.size()];
				      while (orderIter.MoveNext()) 
				      {
					      order = (Order) orderIter.Current ;//.next();
					      if (order.getCustomerPtr() == customerPtr) 
					      {
						      orderCount++;
						      lastOrders[orderCount] = order;
					      }
				      }
                        } //lock
				for (i = orderCount; i > 0; i--) 
				{
					if (orderCount - i < lastOrdersQuantity) 
					{
						orderDates[orderCount - i] = lastOrders[i].getEntryTime();
						orderAmount[orderCount - i] = lastOrders[i].getTotalAmount();
					}
					else 
					{
						break;
					}
				}
				reportTime = DateTime.Now ;//new Date();
			}

			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, 
				"Exiting spec.jbb.CustomerReportTransaction::process");
			return true;
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("CustomerTransaction Display *********************");
                Console.WriteLine("Warehouse ID is " + warehouseId
                            + " ******************");
                Console.WriteLine("District  ID is " + districtId
                            + " ******************");
                Console.WriteLine("Customer  ID is " + customerId
                            + " ******************");
                Console.WriteLine("**************************************************");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void initializeTransactionLog()
        {
            lock (_syncRoot)
            {
                // "00000000001111111111222222222233333333334444444444555555555566666666667777777777"
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // " CUSTOMER REPORT"
                // "Date: DD-MM-YYYY hh:mm:ss"
                // "Warehouse: 9999 District: 99"
                // + "XXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXX"
                // + "XXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXX"
                // + "XXXXXXXXXXXXXXXXXXXX XX XXXXX-XXXX XXXXXXXXXXXXXXXXXXXX XX
                // XXXXX-XXXX"
                // + "\n"
                // + "Customer: 9999 Cust-Warehouse: 9999 Cust-District: 99"
                // + "Name: XXXXXXXXXXXXXXXX XX XXXXXXXXXXXXXXXX Since: DD-MM-YYYY"
                // + " XXXXXXXXXXXXXXXXXXXX Credit: XX"
                // + " XXXXXXXXXXXXXXXXXXXX %Disc: 99.99"
                // + "Last payments Date: Amount:"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + "Last orders Date: Amount:"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + " DD-MM-YYYY hh:mm:ss $9999.99"
                // + "\n\n";
                //JBButil.getLog().entering("spec.jbb.CustomerReportTransaction","initializeTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                "Entering spec.jbb.CustomerReportTransaction::initializeTransactionLog");
                if (Transaction.enableLogWrite)
                    initLog.display();
                //JBButil.getLog().exiting("spec.jbb.CustomerReportTransaction","initializeTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "Exiting spec.jbb.CustomerReportTransaction::initializeTransactionLog");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void processTransactionLog()
        {
            lock (_syncRoot)
            {
                //JBButil.getLog().entering("spec.jbb.CustomerReportTransaction","processTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Entering spec.jbb.CustomerReportTransaction::processTransactionLog");
                customerLog.clearBuffer();
                xmlCustomerLog.clear();
                setupCustomerLog();
                Address ware_addr = warehousePtr.getAddress();
                Address dist_addr = districtPtr.getAddress();
                // String phone;
                String zip;
                // "00000000001111111111222222222233333333334444444444555555555566666666667777777777"
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // " CUSTOMER REPORT"
                // "Date: DD-MM-YYYY hh:mm:ss"
                customerLog.putDate(ref reportTime, 6, 1, 10);
                customerLog.putTime(ref reportTime, 17, 1, 8);
                // "Warehouse: 9999 District: 99"
                customerLog.putInt(warehouseId, 11, 3, 4);
                customerLog.putInt(districtId, 51, 3, 2);
                // + "XXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXX"
                customerLog.putText(ware_addr.getStreet1(), 0, 4, 20);
                customerLog.putText(dist_addr.getStreet1(), 41, 4, 20);
                // + "XXXXXXXXXXXXXXXXXXXX XXXXXXXXXXXXXXXXXXXX"
                customerLog.putText(ware_addr.getStreet2(), 0, 5, 20);
                customerLog.putText(dist_addr.getStreet2(), 41, 5, 20);
                // + "XXXXXXXXXXXXXXXXXXXX XX XXXXX-XXXX XXXXXXXXXXXXXXXXXXXX XX
                // XXXXX-XXXX"
                customerLog.putText(ware_addr.getCity(), 0, 6, 20);
                customerLog.putText(ware_addr.getState(), 21, 6, 2);
                zip = ware_addr.getZip();
                customerLog.putSubstring(zip, 24, 6, 0, 5);
                customerLog.putChar('-', 29, 6);
                customerLog.putSubstring(zip, 30, 6, 5, 4);
                customerLog.putText(dist_addr.getCity(), 41, 6, 20);
                customerLog.putText(dist_addr.getState(), 62, 6, 2);
                zip = dist_addr.getZip();
                customerLog.putSubstring(zip, 65, 6, 0, 5);
                customerLog.putChar('-', 70, 6);
                customerLog.putSubstring(zip, 71, 6, 5, 4);
                // + "\n"
                // + "Customer: 9999 Cust-Warehouse: 9999 Cust-District: 99"
                if (customerPtr != null)
                {
                    Address cust_addr = customerPtr.getAddress();
                    DateTime custDate = customerPtr.getSince();
                    customerLog.putInt(customerPtr.getId(), 10, 8, 4);
                    customerLog.putInt(customerPtr.getWarehouseId(), 32, 8, 4);
                    customerLog.putInt(customerPtr.getDistrictId(), 53, 8, 2);
                    // + "Name: XXXXXXXXXXXXXXXX XX XXXXXXXXXXXXXXXX Since: DD-MM-YYYY"
                    customerLog.putText(customerPtr.getFirstName(), 8, 9, 16);
                    customerLog.putText(customerPtr.getMiddleName(), 25, 9, 2);
                    customerLog.putText(customerPtr.getLastName(), 28, 9, 16);
                    customerLog.putDate(ref custDate, 57, 9, 10);
                    // + " XXXXXXXXXXXXXXXXXXXX Credit: XX"
                    customerLog.putText(cust_addr.getStreet1(), 8, 10, 20);
                    customerLog.putText(customerPtr.getCreditStatus(), 57, 10, 2);
                    // + " XXXXXXXXXXXXXXXXXXXX %Disc: 99.99"
                    customerLog.putText(cust_addr.getStreet2(), 8, 11, 20);
                    //customerLog.putDouble(customerPtr.getDiscountRate().movePointRight(2).toString(), 57, 11, 5);
                    customerLog.putDouble((customerPtr.getDiscountRate() * 100).ToString(), 57, 11, 5);

                    // + "Last payments Date: Amount:"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    for (int i = 0; i < lastPaymentsQuantity; i++)
                    {
                        //if (paymentDates[i].Equals(null))
                        //{
                        customerLog.putDate(ref paymentDates[i], 22, 13 + i, 10);
                        customerLog.putTime(ref paymentDates[i], 33, 13 + i, 8);
                        customerLog.putDollars(paymentAmount[i], 45, 13 + i, 8);
                        //}
                        //else 
                        //{
                        //	break;
                        //}
                    }
                    // + "Last orders Date: Amount:"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    // + " DD-MM-YYYY hh:mm:ss $9999.99"
                    for (int i = 0; i < lastOrdersQuantity; i++)
                    {
                        //if (orderDates[i].Equals(null)) 
                        //{
                        customerLog.putDate(ref orderDates[i], 22, 19 + i, 10);
                        customerLog.putTime(ref orderDates[i], 33, 19 + i, 8);
                        customerLog.putDollars(orderAmount[i], 45, 19 + i, 8);
                        //}
                        //else 
                        //{
                        //	break;
                        //}
                    }
                }
                // create XML representation
                xmlCustomerLog.populateXML(customerLog);
                if (Transaction.enableLogWrite)
                    customerLog.display();
                if (Transaction.validationFlag)
                {
                    String[] s = customerLog.validate();
                    if (s.Length != validationScreen.Length)
                    {
                        Console.WriteLine("VALIDATION ERROR:  mismatch in screen lengths for CustomerReportTransaction");
                        Console.WriteLine("    Screen length should be:  "
                                    + validationScreen.Length);
                        Console.WriteLine("    Screen length is:  " + s.Length);
                        Transaction.invalidateRun();
                    }
                    for (int i = 0; i < validationScreen.Length; i++)
                    {
                        if (checkLine[i])
                        {
                            if (!s[i].Equals(validationScreen[i]))
                            {
                                Console.WriteLine("VALIDATION ERROR:  incorrect output for CustomerReportTransaction");
                                Console.WriteLine("    Line " + (i + 1)
                                            + " should be:  |" + validationScreen[i] + "|");
                                Console.WriteLine("    Line " + (i + 1) + " is:  |"
                                            + s[i] + "|");
                                Transaction.invalidateRun();
                            }
                        }
                    }
                }
                //JBButil.getLog().exiting("spec.jbb.CustomerReportTransaction","processTransactionLog");
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose, "Exiting spec.jbb.CustomerReportTransaction::processTransactionLog");
            }
        }
	}
}
