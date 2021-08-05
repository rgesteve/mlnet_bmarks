/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC)
 * 
 * All rights reserved.
 * 
 * Copyright (c) 1996-2005 IBM Corporation, Inc. All rights reserved.
 * 
 */
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Order.
	/// </summary>
	public class Order
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Company     company;

		private Customer    customerPtr;

		private Orderline[] orderlineList;

		private Decimal     totalAmount;

		private Decimal     customerDiscountRate;

		private Decimal     districtTax;

		private Decimal     warehouseTax;

		private DateTime    entryDateTime;

		private int         orderId;

		private short       customerId;

		private short       carrierId;

		private short       orderLineCount;

		private sbyte       districtId;

		private short       warehouseId;

		private bool        allLocal;

		public Order(Company inCompany, int inOrderId, sbyte inDistrictId,
			short inWarehouseId, short inCustomerId, Customer inCustomerPtr,
			Decimal inDistrictTaxRate, Decimal inWarehouseTaxRate,
			Decimal inCustomerDiscountRate) 
		{
			this.initOrder(inCompany, inOrderId, inDistrictId, inWarehouseId,
				inCustomerId, inCustomerPtr, inDistrictTaxRate,
				inWarehouseTaxRate, inCustomerDiscountRate);
		}

		public void initOrder(Company inCompany, int inOrderId, sbyte inDistrictId,
			short inWarehouseId, short inCustomerId, Customer inCustomerPtr,
			Decimal inDistrictTaxRate, Decimal inWarehouseTaxRate,
			Decimal inCustomerDiscountRate) 
		{
			company = inCompany;
			orderId = inOrderId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
			customerId = inCustomerId;
			customerPtr = inCustomerPtr;
			orderLineCount = 0;
			orderlineList = new Orderline[(Transaction.aveOrderlines + 10)];
			//if (JBButil.getLog().isLoggable(Level.FINEST)) 
			Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"Order::initOrder  orderlineList=" + orderlineList);
			//}
			entryDateTime = DateTime.Now;//new Date();
			carrierId = 0;
			allLocal = true;
			districtTax = inDistrictTaxRate;
			warehouseTax = inWarehouseTaxRate;
			customerDiscountRate = inCustomerDiscountRate;
		}
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool processLines(Warehouse inWarehousePtr,
                                        short number_of_orderlines, bool rollback)
        {
            lock (_syncRoot)
            {
                short supplyWarehouseId;
                bool processLinesResult = true;
                Decimal amount = Decimal.Zero;//new Decimal(0,0,0,false,2);//BigDecimal.valueOf(0, 2);
                for (short LineNumber = 1; LineNumber <= number_of_orderlines; ++LineNumber)
                {
                    Orderline newOrderline = new Orderline(company, orderId,
                        districtId, warehouseId, LineNumber, number_of_orderlines,
                        rollback);
                    supplyWarehouseId = warehouseId;
                    if ((JBButil.random(1, 100, warehouseId) == 1)
                        && (company.getMaxWarehouses() > 1)) // Comment #2
                    {
                        while (supplyWarehouseId == warehouseId)
                        {
                            supplyWarehouseId = (short)JBButil.random(1, company
                                .getMaxWarehouses(), warehouseId);
                        }
                        allLocal = false;
                    }
                    newOrderline.setSupplyWarehouse(supplyWarehouseId);
                    if (newOrderline.validateAndProcess(inWarehousePtr))
                    {
                        amount = Decimal.Add(amount, newOrderline.getAmount());//amount.add(newOrderline.getAmount());
                        orderlineList[orderLineCount] = newOrderline;
                        orderLineCount++;
                    }
                    else
                    {
                        processLinesResult = false;
                    }
                }
                //BigDecimal subtotal = amount.multiply((BigDecimal.valueOf(1, 0)).subtract(customerDiscountRate)).setScale(2, BigDecimal.ROUND_HALF_UP);
                Decimal subtotal = Math.Round(Decimal.Multiply(amount, Decimal.Subtract(Decimal.One, customerDiscountRate)), 2);
                //Decimal subtotal = Math.Round(Decimal.Subtract(Decimal.Multiply(amount, Decimal.One), customerDiscountRate),2) ;
                //totalAmount = subtotal.multiply((BigDecimal.valueOf(1, 0)).add(warehouseTax).add(districtTax)).setScale(2, BigDecimal.ROUND_HALF_UP);
                //totalAmount = Decimal.Round(Decimal.Add(Decimal.Multiply(subtotal, Decimal.One),Decimal.Add(districtTax, warehouseTax)),2);
                totalAmount = Math.Round(Decimal.Multiply(subtotal, Decimal.Add(Decimal.Add(Decimal.One, warehouseTax), districtTax)), 2);
                return processLinesResult;
            }
        }

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void dateOrderlines(DateTime orderdate)
        {
            lock (_syncRoot)
            {
                int i;
                Orderline orderline;
                //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                //{
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "Order::dateOrderlines  orderlineList=" + orderlineList
                    + " orderlineCount=" + orderLineCount);
                //}
                for (i = 0; i < orderLineCount; i++)
                {
                    orderline = orderlineList[i];
                    //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                    //{
                    Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                        " orderline=" + orderline + " orderline#=" + i);
                    //}
                    orderline.setDeliveryTime(orderdate);
                }
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int getId()
        {
            lock (_syncRoot)
            {
                int temp = orderId;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Customer getCustomerPtr()
        {
            lock (_syncRoot)
            {
                Customer temp = customerPtr;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Decimal getTotalAmount()
        {
            lock (_syncRoot)
            {
                Decimal temp = totalAmount;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public DateTime getEntryTime()
        {
            lock (_syncRoot)
            {
                DateTime temp = entryDateTime;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setCarrierId(short inCarrierId)
        {
            lock (_syncRoot)
            {
                carrierId = inCarrierId;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public short getCarrierId()
        {
            lock (_syncRoot)
            {
                short temp = carrierId;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Orderline[] getOrderlineList()
        {
            lock (_syncRoot)
            {           //if (JBButil.getLog().isLoggable(Level.FINEST)) 
                        //{
                Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
                    "Order::getOrderlineList  orderlineList=" + orderlineList);
                //}
                return orderlineList;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public short getOrderlineCount()
        {
            lock (_syncRoot)
            {
                short temp = orderLineCount;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setAllLocal(bool inAllLocal)
        {
            lock (_syncRoot)
            {
                allLocal = inAllLocal;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setEntryDateTime(DateTime inEntryDateTime)
        {
            lock (_syncRoot)
            {
                entryDateTime = inEntryDateTime;
            }
        }
	}
}
