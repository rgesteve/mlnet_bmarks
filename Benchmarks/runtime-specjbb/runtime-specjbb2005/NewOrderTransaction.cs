using System;
using System.Diagnostics;
using Specjbb2005.src.spec.jbb.infra.Util;
using System.Runtime.CompilerServices;
using System.Text;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for NewOrderTransaction.
	/// </summary>
	public class NewOrderTransaction : Transaction
	{
		// This goes right after each class/interface statement
		//  static readonly String          COPYRIGHT     = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Company              company;

		private short                warehouseId;

		private sbyte                districtId;

		private short                customerId;

		private Warehouse            warehousePtr;

		private District             districtPtr;

		private short                number_of_orderlines;

		private bool                 rollback;

		private Decimal              warehouseTaxRate;

		private Decimal              districtTaxRate;

		private int                  orderId;

		private Decimal              customerDiscountRate;

		private String               customerLastName;

		private String               customerCreditStatus;

		private short                orderline_count;

		private Order                thisOrder;

		private TransactionLogBuffer backLog;

		private TransactionLogBuffer initLog;

		private TransactionLogBuffer orderLog;

		private XMLTransactionLog    xmlOrderLog;


		internal String[]                     validationLog = 
									{
										"                                   New Order",
										"Warehouse:    1   District:  6                        Date: 13-04-2000 10:10:51",
										"Customer:    17   Name: BAROUGHTANTI       Credit: GC   %Disc: 43.11",
										"Order Number:       31  Number of Lines: 15        W_tax: 10.30   D_tax:  6.62",
										"",
										" Supp_W  Item_Id  Item Name                 Qty  Stock  B/G  Price    Amount   ",
										"     1    12117   rk9rIQP9q52FiN4FWuihzNT    5     61    G    $61.73   $308.65 ",
										"     1    19072   0VOgv21kW1ZS3pv1IzirxrmH   5     91    G    $26.62   $133.10 ",
										"     1     4260   clRd7h9zm9eGwqtvKblk1q     5     81    G    $85.08   $425.40 ",
										"     1     5772   b96nAb1I7qPaOA             5     85    G    $50.24   $251.20 ",
										"     1      587   98ILzFnfHy8rwch            5     79    G     $3.44    $17.20 ",
										"     1     2790   yrZXzgfPRKxNQW             5     96    G    $33.70   $168.50 ",
										"     1      687   G1GcZl0h1P7gJ4GxZhnuT      5     88    G    $46.26   $231.30 ",
										"     1     5258   b9PfVfw5TcKQe0iEZaPEWkt    5     57    G    $45.62   $228.10 ",
										"     1    12238   w0Tu7bleXb7XXY03           5     42    G    $70.20   $351.00 ",
										"     1     4933   TvoMqL3eITHLzearKdECH      5     41    G    $93.19   $465.95 ",
										"     1     4186   LNDK2oajzLA29ICevp8eBx     5     76    G    $49.22   $246.10 ",
										"     1     6996   450trcQ3zvuPwWHGb6i        5     56    G     $6.11    $30.55 ",
										"     1     9925   Vh0KacEuowjiT8ey           5     30    G    $72.28   $361.40 ",
										"     1    12042   m7ryJMNGNEaOaKEG           5     90    G    $65.31   $326.55 ",
										"     1     9471   hAdDzcbhMhlOQTUecrfFCym    5     78    G    $68.54   $342.70 ",
										"Execution Status: ---- Order is valid ----                   Total:   $2585.93 ",
										"", ""
									};

		internal bool[]                    checkLine     = {
														 true, false, true, true, true, true, true, true, true, true, false,
														 true, true, true, true, true, true, false, true, true, true, true,
														 true, true
													 };

		public NewOrderTransaction(Company inCompany, short inWarehouseId) 
		{
			company = inCompany;
			// 2.4.1.1
			warehouseId = inWarehouseId;
			warehousePtr = company.getWarehousePtr(warehouseId, false);
			orderLog = new TransactionLogBuffer();
			setupOrderLog();
			xmlOrderLog = new XMLTransactionLog();
			initLog = new TransactionLogBuffer(orderLog);
			setupInitLog();
		}

		private void setupOrderLog() 
		{
			orderLog.putText("New Order", 35, 0, 9);
			orderLog.putText("Warehouse:", 0, 1, 10);
			orderLog.putInt(warehouseId, 11, 1, 4);
			orderLog.putText("District:", 18, 1, 9);
			orderLog.putText("Date:", 54, 1, 5);
			orderLog.putText("Customer:", 0, 2, 10);
			orderLog.putText("Name:", 18, 2, 5);
			orderLog.putText("Credit:", 43, 2, 7);
			orderLog.putText("%Disc:", 56, 2, 6);
			orderLog.putText("Order Number:", 0, 3, 14);
			orderLog.putText("Number of Lines:", 24, 3, 26);
			orderLog.putText("W_tax:", 51, 3, 6);
			orderLog.putText("D_tax:", 66, 3, 6);
			orderLog
				.putText(
				" Supp_W  Item_Id  Item Name                 Qty  Stock  B/G  Price    Amount",
				0, 5, 79);
			orderLog.putText("Execution Status:", 0, 21, 17);
			orderLog.putText("Total:", 61, 21, 6);
		}

		private void setupInitLog() 
		{
			// initScreen.putCharFill('9', 11, 1, 4);
			initLog.putCharFill('9', 28, 1, 2);
			initLog.putText("DD-MM-YYYY hh:mm:ss", 60, 1, 19);
			initLog.putCharFill('9', 11, 2, 4);
			initLog.putCharFill('X', 24, 2, 16);
			initLog.putCharFill('X', 51, 2, 2);
			initLog.putText("99.99", 63, 2, 5);
			initLog.putCharFill('9', 14, 3, 8);
			initLog.putCharFill('9', 41, 3, 2);
			initLog.putText("99.99", 58, 3, 5);
			initLog.putText("99.99", 73, 3, 5);
			for (int i = 6; i < 21; i++) 
			{
				initLog.putCharFill('9', 2, i, 4);
				initLog.putCharFill('9', 9, i, 6);
				initLog.putCharFill('X', 18, i, 24);
				initLog.putCharFill('9', 44, i, 2);
				initLog.putCharFill('9', 50, i, 3);
				initLog.putChar('X', 57, i);
				initLog.putText("$999.99", 61, i, 7);
				initLog.putText("$9999.99", 70, i, 8);
			}
			initLog.putCharFill('X', 18, 21, 24);
			initLog.putText("$99999.99", 69, 21, 9);
		}

		public override String getMenuName() 
		{
			return "New-Order";
		}

		public void delete() 
		{

		}

		public override void init() 
		{
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose, 
					"entering spec.jbb.NewOrderTransaction::init");
			districtId = (sbyte) (JBButil.random(1, company
				.getMaxDistrictsPerWarehouse(), warehouseId));
			customerId = JBButil.create_random_customer_id(company
				.getMaxCustomersPerDistrict(), warehouseId);
			number_of_orderlines = (short) JBButil.random(
				Transaction.minOrderlines, Transaction.maxOrderlines,
				warehouseId);
			rollback = (bool) (JBButil.random(1, 100, warehouseId) == 1);
			rollback = false;
			districtPtr = warehousePtr.getDistrictPtr(districtId, false);
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
						"exiting spec.jbb.NewOrderTransaction::init");
		}

		public override bool process() 
		{
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"entering spec.jbb.NewOrderTransaction::process");
			NewOrder theNewOrder;
			warehouseTaxRate = warehousePtr.getTaxRate();
			districtTaxRate = districtPtr.getTaxRate();
			orderId = districtPtr.nextOrderId();
			long uniqueCustomerId = company.buildUniqueCustomerKey(warehouseId,
				districtId, customerId);
			Customer customerPtr = company.getCustomer(uniqueCustomerId, false);
			customerDiscountRate = customerPtr.getDiscountRate();
			customerLastName = customerPtr.getLastName();
			customerCreditStatus = customerPtr.getCreditStatus();
			thisOrder = new Order(company, orderId, districtId, warehouseId,
				customerId, customerPtr, districtTaxRate, warehouseTaxRate,
				customerDiscountRate);
			theNewOrder = new NewOrder(thisOrder, orderId, districtId, warehouseId);
			if (thisOrder
				.processLines(warehousePtr, number_of_orderlines, rollback)) 
			{
				orderline_count = thisOrder.getOrderlineCount();
				districtPtr.addOrder(thisOrder);
				districtPtr.addNewOrder(theNewOrder);
				customerPtr.addOrder(thisOrder);
			}
			else 
			{
				orderline_count = 0;
			}
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
				"exiting spec.jbb.NewOrderTransaction::process");
			return true;
		}

        private readonly object _syncRoot = new Object(); // CORECLR
                                                          // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void display()
        {
            lock (_syncRoot)
            {
                Console.WriteLine("**** NewOrderTransaction Display **********************");
                Console.WriteLine("*       Warehouse ID is " + warehouseId
                            + "                      *");
                Console.WriteLine("*       District  ID is " + districtId
                            + "                      *");
                Console.WriteLine("*       Customer  ID is " + customerId
                            + "                      *");
                Console.WriteLine("*******************************************************");
            }
        }

		public override void initializeTransactionLog() 
		{
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"entering spec.jbb.NewOrderTransaction::initializeTransactionLog");
			// " 1 2 3 4 5 6 7 "
			// "01234567890123456789012345678901234567890123456789012345678901234567890123456789"
			// " NEW ORDER "
			// + "Warehouse: 9999 District: 99 Date: DD-MM-YYYY hh:mm:ss"
			// + "Customer: 9999 Name: XXXXXXXXXXXXXXXX Credit: XX %Disc: 99.99"
			// + "Order Number: 99999999 Number of Lines: 99 W_tax: 99.99 D_tax:
			// 99.99"
			// + " Supp_W Item_Id Item Name Qty Stock B/G Price Amount"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + " 9999 999999 XXXXXXXXXXXXXXXXXXXXXXXX 99 999 X $999.99 $9999.99"
			// + "Execution Status: XXXXXXXXXXXXXXXXXXXXXXXX Total: $99999.99";
			if (Transaction.enableLogWrite)
				initLog.display();
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
				"exiting spec.jbb.NewOrderTransaction::initializeTransactionLog");
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public override void processTransactionLog()
        {
            lock (_syncRoot)
            {
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                "entering spec.jbb.NewOrderTransaction::processTransactionLog");
                orderLog.clearBuffer();
                xmlOrderLog.clear();
                setupOrderLog();
                DateTime screenDate = thisOrder.getEntryTime();
                orderLog.putInt(districtId, 28, 1, 2);
                orderLog.putDate(ref screenDate, 60, 1, 10);
                orderLog.putTime(ref screenDate, 71, 1, 8);
                // + "Customer: 9999 Name: XXXXXXXXXXXXXXXX Credit: XX %Disc: 99.99"
                orderLog.putInt(customerId, 11, 2, 4);
                orderLog.putText(customerLastName, 24, 2, 16);
                orderLog.putText(customerCreditStatus, 51, 2, 2);

                //orderLog.putDouble(customerDiscountRate.movePointRight(2).ToString(),63, 2, 5);
                //since we don't have equivalent of movepointRight, just multiply by 100. Milind
                orderLog.putDouble((customerDiscountRate * 100).ToString(), 63, 2, 5);
                orderLog.putInt(orderId, 14, 3, 8);
                orderLog.putInt(orderline_count, 41, 3, 2);

                Decimal temp = Decimal.Multiply(warehouseTaxRate, 100);
                orderLog.putDouble(temp.ToString(), 58, 3, 5);

                Decimal temp1 = Decimal.Multiply(districtTaxRate, 100);
                orderLog.putDouble(temp1.ToString(), 73, 3, 5);
                if (orderline_count > 0)
                {
                    Orderline[] orderlineList;
                    int lineCount, i;
                    orderlineList = thisOrder.getOrderlineList();
                    lineCount = thisOrder.getOrderlineCount();
                    Orderline currentOrderLine;
                    for (i = 0; i < lineCount; i++)
                    {
                        int displayLine = i + 6;
                        if (i >= 15)
                            displayLine = 14 + 6;
                        currentOrderLine = (Orderline)orderlineList[i];
                        orderLog.putInt(currentOrderLine.getSupplyWarehouse(), 2,
                            displayLine, 4);
                        orderLog
                            .putInt(currentOrderLine.getItemId(), 9, displayLine, 6);
                        orderLog.putText(currentOrderLine.getItemName(), 18,
                            displayLine, 24);
                        orderLog.putInt(currentOrderLine.getQuantity(), 44,
                            displayLine, 2);
                        orderLog.putInt(currentOrderLine.getStockQuantity(), 50,
                            displayLine, 3);
                        orderLog.putChar(currentOrderLine.getBrandGeneric(), 57,
                            displayLine);
                        orderLog.putDollars(currentOrderLine.getItemPrice(), 62,
                            displayLine, 7);
                        orderLog.putDollars(currentOrderLine.getAmount(), 71,
                            displayLine, 8);
                    }
                }
                if (orderline_count > 0)
                {
                    // + "Execution Status: XXXXXXXXXXXXXXXXXXXXXXXX Total: $99999.99";
                    orderLog.putText("---- Order is valid ----", 18, 21, 24);
                    orderLog.putDollars(thisOrder.getTotalAmount(), 70, 21, 9);
                }
                else
                {
                    // + "Execution Status: ITEM NUMBER IS NOT VALID Total: $.00");
                    orderLog.putText("ITEM NUMBER IS NOT VALID", 18, 21, 24);
                    orderLog.putDollars(0.0, 69, 21, 9);
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
                        sb.Append("VALIDATION ERROR:  mismatch in screen lengths for NewOrderTransaction");
                        sb.Append(Environment.NewLine/*System.getProperty("line.separator")*/);
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
                                sb.Append("VALIDATION ERROR:  incorrect output for NewOrderTransaction");
                                sb.Append(Environment.NewLine/*System.getProperty("line.separator")*/);
                                sb.Append("    Line " + (i + 1) + " should be:  |"
                                    + validationLog[i] + "|");
                                sb.Append(Environment.NewLine/*System.getProperty("line.separator")*/);
                                sb.Append("    Line " + (i + 1) + " is:  |" + s[i]
                                    + "|");
                                Trace.WriteLineIf(JBButil.getLog().TraceWarning, sb.ToString());//JBButil.getLog().warning(sb.ToString());
                                Transaction.invalidateRun();
                            }
                        }
                    }
                }
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        "exiting spec.jbb.NewOrderTransaction::processTransactionLog");
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void processPreloadedOrders()
        {
            lock (_syncRoot)
            {
                orderId = districtPtr.nextOrderId();
                long uniqueCustomerId = company.buildUniqueCustomerKey(warehouseId,
                    districtId, customerId);
                Customer customerPtr = company.getCustomer(uniqueCustomerId, false);
                //Do we really need a Decimal.Round here?
                Decimal temp = Math.Round(Decimal.Zero, 2);//new Decimal(0,0,0,false,2) ;//Decimal.valueOf(0, 2);
                thisOrder = new Order(company, orderId, districtId, warehouseId,
                    customerId, customerPtr, temp, temp, temp);
                districtPtr.addOrder(thisOrder); // place pointer to self in
                                                 // district's
                                                 // orderTable
                customerPtr.addOrder(thisOrder); // update customer's order list
                DateTime orderdate = DateTime.Now;//new Date();
                thisOrder.setEntryDateTime(orderdate);
                if (orderId <= (company.getInitialOrders() - company
                    .getInitialNewOrders())) // should be < 2101
                {
                    sbyte carrierId = (sbyte)JBButil.random(1, 10, warehouseId);
                    thisOrder.setCarrierId(carrierId);
                }
                thisOrder.setAllLocal(true);
                thisOrder.processLines(warehousePtr, number_of_orderlines, false);
                orderline_count = thisOrder.getOrderlineCount();
                if (orderId > (company.getInitialOrders() - company
                    .getInitialNewOrders())) // should be > 2100
                {
                    NewOrder theNewOrder = new NewOrder(thisOrder, orderId, districtId,
                        warehouseId);
                    districtPtr.addNewOrder(theNewOrder); // place pointer to self in
                                                          // district's orderTable
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setDistrictandCustomer(sbyte inDistrictId,
                                short inCustomerId)
        {
            lock (_syncRoot)
            {
                districtId = inDistrictId;
                customerId = inCustomerId;
                districtPtr = warehousePtr.getDistrictPtr(districtId, false);
            }
        }
	}
}
