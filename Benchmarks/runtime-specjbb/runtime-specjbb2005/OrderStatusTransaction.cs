/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Diagnostics;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;
using System.Text;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for OrderStatusTransaction.
	/// </summary>
	public class OrderStatusTransaction : Transaction
	{
		// This goes right after each class/interface statement
		//  static readonly String          COPYRIGHT     = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Company              company;

		private short                warehouseId;

		private sbyte                 districtId;

		private String               cust_last_name;

		private bool                 use_customerId;

		private short                customerId;

		private Customer             customerPtr;

		private Decimal              customerBalance;

		private String               customerFirstName;

		private String               customerMiddleName;

		private String               customerLastName;

		private Warehouse            warehousePtr;

		private District             districtPtr;

		private Order                currentOrderPtr;

		private int                  orderId;

		private DateTime             orderEntryDate;

		private short                orderCarrierId;

		private int                  orderLineCount;

		private short[]              ol_supplying_warehouse;

		private int[]                ol_item_id;

		private String[]             ol_item_name;

		private short[]              ol_quantity;

		private int[]                ol_stock;

		private char[]               ol_b_g;

		private Decimal[]            ol_item_price;

		private Decimal[]            ol_amount;

		private DateTime[]           ol_delivery_date;

		private TransactionLogBuffer orderLog;

		private TransactionLogBuffer initLog;

		private XMLTransactionLog    xmlOrderLog;

		String[]                     validationLog = {
														 "                                  Order-Status",
														 "Warehouse:    1   District:  5",
														 "Customer:    4    Name:wo8iWcPkuJ5Fy    OE BARBARPRI       ",
														 "Cust-Balance:     -/0.00",
														 "",
														 "Order-Number:        4   Entry-Date: 13-04-2000 11:03:13   Carrier-Number: 10",
														 "Supply-W     Item-Id    Qty      Amount     Delivery-Date   ",
														 "     1        12578      5        $402.55  ",
														 "     1         1515      5        $385.90  ",
														 "     1        11638      5        $285.00  ",
														 "     1          318      5         $84.70  ",
														 "     1        13417      5        $388.25  ",
														 "     1        16614      5        $286.60  ",
														 "     1        10277      5        $415.45  ",
														 "     1         7598      5        $263.10  ",
														 "     1        18812      5        $239.95  ",
														 "     1         2304      5        $333.45  ",
														 "     1        12801      5         $61.30  ",
														 "     1          852      5        $105.35  ",
														 "     1         4282      5         $77.10  ",
														 "     1        11977      5        $341.55  ",
														 "     1         7411      5        $418.35  ", "", ""
													 };

		bool[]                    checkLine     = {
														 true, true, true, false, true, false, true, true, true, true,
														 false, true, true, true, true, true, true, false, true, false,
														 true, true, true, true
												};
		
		public OrderStatusTransaction(Company inCompany, short inWarehouseId) // 2.6.1.1
		{
			company = inCompany;
			warehouseId = inWarehouseId;
			ol_supplying_warehouse = new short[30];
			ol_item_id = new int[30];
			ol_item_name = new String[30];
			ol_quantity = new short[30];
			ol_stock = new int[30];
			ol_b_g = new char[30];
			ol_item_price = new Decimal[30];
			ol_amount = new Decimal[30];
			ol_delivery_date = new DateTime[30];
			orderLog = new TransactionLogBuffer();
			setupOrderLog();
			xmlOrderLog = new XMLTransactionLog();
			initLog = new TransactionLogBuffer(orderLog);
			setupInitLog();
		}

		private void setupOrderLog() 
		{
			orderLog.putText("Order-Status", 34, 0, 12);
			orderLog.putText("Warehouse:", 0, 1, 10);
			orderLog.putInt(warehouseId, 11, 1, 4);
			orderLog.putText("District:", 18, 1, 9);
			orderLog.putText("Customer:", 0, 2, 10);
			orderLog.putText("Name:", 18, 2, 5);
			orderLog.putText("Cust-Balance:", 0, 3, 14);
			orderLog.putText("Order-Number:", 0, 5, 14);
			orderLog.putText("Entry-Date:", 25, 5, 12);
			orderLog.putText("Carrier-Number:", 59, 5, 16);
			orderLog.putText(
				"Supply-W     Item-Id    Qty      Amount     Delivery-Date", 0,
				6, 60);
		}

		private void setupInitLog() 
		{
			// initScreen.putCharFill('9', 11, 1, 4);
			initLog.putCharFill('9', 28, 1, 2);
			initLog.putCharFill('9', 10, 2, 4);
			initLog.putCharFill('X', 23, 2, 16);
			initLog.putCharFill('X', 40, 2, 2);
			initLog.putCharFill('X', 43, 2, 16);
			initLog.putText("$-99999.99", 14, 3, 10);
			initLog.putCharFill('9', 14, 5, 8);
			initLog.putText("DD-MM-YYYY", 37, 5, 10);
			initLog.putText("hh:mm:ss", 48, 5, 8);
			initLog.putCharFill('9', 75, 5, 2);
			for (int i = 7; i < 22; i++) 
			{
				initLog.putCharFill('9', 2, i, 4);
				initLog.putCharFill('9', 13, i, 6);
				initLog.putCharFill('9', 24, i, 2);
				initLog.putText("$99999.99", 32, i, 9);
				initLog.putText("DD-MM-YYYY", 46, i, 10);
			}
		}

		public override String getMenuName() 
		{
			return "Order-Status";
		}

        private readonly object _syncRoot = new Object(); // CORECLR

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
                        "entering spec.jbb.OrderStatusTransaction::init");
                districtId = (sbyte)JBButil.random(1, company
                    .getMaxDistrictsPerWarehouse(), warehouseId);
                int y = (int)JBButil.random(1, 100, warehouseId);
                if (y <= 60)
                {
                    cust_last_name = JBButil.choose_random_last_name(company
                        .getMaxCustomersPerDistrict(), warehouseId);
                    use_customerId = false;
                }
                else
                {
                    customerId = JBButil.create_random_customer_id(company
                        .getMaxCustomersPerDistrict(), warehouseId);
                    use_customerId = true;
                }
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.OrderStatusTransaction::init");
            }
        }
		public override bool process() 
		{
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"entering spec.jbb.OrderStatusTransaction::process");
			int line_number = 0;
			if (use_customerId) 
			{
				long uniqueCustomerId = company.buildUniqueCustomerKey(warehouseId,
					districtId, customerId);
				customerPtr = company.getCustomer(uniqueCustomerId, false);
			}
			else 
			{
				customerPtr = company.getCustomerByLastName(warehouseId,
                    districtId, cust_last_name);
			}
			if (customerPtr != null) 
			{
				customerId = customerPtr.getId();
				customerBalance = customerPtr.getBalance();
				customerFirstName = customerPtr.getFirstName();
				customerMiddleName = customerPtr.getMiddleName();
				customerLastName = customerPtr.getLastName();
				currentOrderPtr = customerPtr.getLatestOrder();
				if (currentOrderPtr != null) 
				{
					orderId = currentOrderPtr.getId();
					orderEntryDate = currentOrderPtr.getEntryTime();
					orderCarrierId = currentOrderPtr.getCarrierId();
					orderLineCount = currentOrderPtr.getOrderlineCount();
					Orderline orderline;
					Orderline[] orderlineList;
					int i;
					orderlineList = currentOrderPtr.getOrderlineList();
					//if (JBButil.getLog().isLoggable(Level.FINEST)) 
					//{
						Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
							"OrderStatusTransaction::process"
							+ " orderlineList=" + orderlineList
							+ " orderLineCount=" + orderLineCount);
					//}
					for (i = 0; i < orderLineCount; i++) 
					{
						orderline = (Orderline) orderlineList[i];
						//if (JBButil.getLog().isLoggable(Level.FINEST)) 
						//{
							Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
								" i=" + i + " line_number=" + line_number
								+ " orderline=" + orderline);
						//}
						ol_supplying_warehouse[line_number] = orderline
							.getSupplyWarehouse();
						ol_item_id[line_number] = orderline.getItemId();
						warehousePtr = company.getWarehousePtr(warehouseId, false);
						ol_item_name[line_number] = (warehousePtr
							.retrieveItem(orderline.getItemId())).getName();
						ol_quantity[line_number] = orderline.getQuantity();
						ol_stock[line_number] = orderline.getStockQuantity();
						ol_b_g[line_number] = orderline.getBrandGeneric();
						ol_item_price[line_number] = orderline.getItemPrice();
						ol_amount[line_number] = orderline.getAmount();
						ol_delivery_date[line_number] = orderline
							.getDeliveryDateTime();
						line_number++;
					}
					Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
						"exiting spec.jbb.OrderStatusTransaction::process");
					return true;
				}
				else 
				{
					Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
						"exiting spec.jbb.OrderStatusTransaction::process");
					return false;
				}
			}
			else 
			{
				customerId = 0;
				customerFirstName = "";
				customerMiddleName = "";
				customerLastName = cust_last_name;
				customerBalance = Decimal.Zero;//BigDecimal.valueOf(0, 2);
				currentOrderPtr = null;
				Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"exiting spec.jbb.OrderStatusTransaction::process");
				return false;
			}
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("OrderStatus Display **********************");
                Console.WriteLine("Warehouse ID is " + warehouseId);
                Console.WriteLine("District  ID is " + districtId);
                Console.WriteLine("Customer  ID is " + customerId);
                Console.WriteLine("**************************************************");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void initializeTransactionLog()
        {
            lock (_syncRoot)
            {
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                "entering spec.jbb.OrderStatusTransaction::initializeTransactionLog");
                DateTime screenDate = DateTime.Now;//new Date();
                                                   // "00000000001111111111222222222233333333334444444444555555555566666666667777777777"
                                                   // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                                                   // " ORDER-STATUS"
                                                   // "Warehouse: 9999 District: 99"
                                                   // "Customer: 9999 Name: xxxxxxxxxxxxxxxx xx xxxxxxxxxxxxxxxx"
                                                   // "Cust-Balance: $-99999.99"
                                                   // ""
                                                   // "Order-Number: 99999999 Entry-Date: DD-MM-YYYY hh:mm:ss
                                                   // Carrier-Number: 99"
                                                   // "Supply-W Item-Id Qty Amount Delivery-Date"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                                                   // ""
                                                   // ""
                orderLog.putDate(ref screenDate, 37, 5, 10);
                if (Transaction.enableLogWrite)
                    initLog.display();
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.OrderStatusTransaction::initializeTransactionLog");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void processTransactionLog()
        {
            lock (_syncRoot)
            {
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                "entering spec.jbb.OrderStatusTransaction::processTransactionLog");
                DateTime screenDate = DateTime.Now;//new Date();
                orderLog.clearBuffer();
                xmlOrderLog.clear();
                setupOrderLog();
                // "00000000001111111111222222222233333333334444444444555555555566666666667777777777"
                // "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
                // " ORDER-STATUS"
                // "Warehouse: 9999 District: 99"
                // orderScreen.putInt(warehouseId, 11, 1, 4);
                orderLog.putInt(districtId, 28, 1, 2);
                // "Customer: 9999 Name: xxxxxxxxxxxxxxxx xx xxxxxxxxxxxxxxxx"
                orderLog.putInt(customerId, 10, 2, 4);
                orderLog.putText(customerFirstName, 23, 2, 16);
                orderLog.putText(customerMiddleName, 40, 2, 2);
                orderLog.putText(customerLastName, 43, 2, 16);
                // "Cust-Balance: $-99999.99"
                // orderScreen.putDollars(customerBalance.doubleValue(), 14, 3, 10);
                orderLog.putDollars(customerBalance, 14, 3, 10);
                // ""
                if (currentOrderPtr != null)
                {
                    DateTime deliveryDate;
                    // "Order-Number: 99999999 Entry-Date: DD-MM-YYYY hh:mm:ss
                    // Carrier-Number: 99"
                    orderLog.putInt(orderId, 14, 5, 8);
                    if (!orderEntryDate.Equals(null))
                    {
                        orderLog.putDate(ref orderEntryDate, 37, 5, 10);
                        orderLog.putTime(ref orderEntryDate, 48, 5, 8);
                    }
                    orderLog.putInt(orderCarrierId, 75, 5, 2);
                    // "Supply-W Item-Id Qty Amount Delivery-Date"
                    for (int line_number = 0; line_number < orderLineCount; line_number++)
                    {
                        // " 9999 999999 99 $99999.99 DD-MM-YYYY"
                        int displayLine = line_number + 7;
                        if (line_number >= 15)
                            displayLine = 15 + 7;
                        orderLog.putInt(ol_supplying_warehouse[line_number], 2,
                            displayLine, 4);
                        orderLog.putInt(ol_item_id[line_number], 13, displayLine, 6);
                        orderLog.putInt(ol_quantity[line_number], 24, displayLine, 2);
                        // orderScreen.putDollars(ol_amount[line_number].doubleValue(),
                        // 32, displayLine,9);
                        orderLog.putDollars(ol_amount[line_number], 34, displayLine, 9);
                        deliveryDate = ol_delivery_date[line_number];
                        if (!deliveryDate.Equals(null))
                            orderLog.putDate(ref deliveryDate, 46, displayLine, 10);
                    }
                }
                else
                // if currentOrderPtr is nil customer has no orders
                {
                    if (customerPtr == null)
                    {
                        orderLog.putText("No customer found for last name.", 2, 4, 32);
                    }
                    else
                    {
                        orderLog.putText("No orders pending.", 2, 7, 20);
                    }
                }
                // create XML representation
                xmlOrderLog.populateXML(orderLog);
                if (Transaction.enableLogWrite)
                    orderLog.display();
                if (Transaction.validationFlag)
                {
                    String[] s = orderLog.validate();
                    if (s.Length != validationLog.Length)
                    {
                        StringBuilder sb = new StringBuilder(200);
                        sb.Append("VALIDATION ERROR:  mismatch in screen lengths for OrderStatusTransaction");
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
                                sb.Append("VALIDATION ERROR:  incorrect output for OrderStatusTransaction");
                                sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                                sb.Append("    Line " + (i + 1) + " should be:  |"
                                    + validationLog[i] + "|");
                                sb.Append(Environment.NewLine);//System.getProperty("line.separator"));
                                sb.Append("    Line " + (i + 1) + " is:  |" + s[i] + "|");
                                Trace.WriteLineIf(JBButil.getLog().TraceWarning, sb.ToString());//JBButil.getLog().warning(sb.ToString());
                                Transaction.invalidateRun();
                            }
                        }
                    }
                }
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "exiting spec.jbb.OrderStatusTransaction::processTransactionLog");
            }
        }

	}
}
