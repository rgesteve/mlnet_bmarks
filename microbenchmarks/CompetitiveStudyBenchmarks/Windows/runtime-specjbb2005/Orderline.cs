/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Runtime.CompilerServices;
using Specjbb2005.src.spec.jbb.infra.Util;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Orderline.
	/// </summary>
	public class Orderline
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		// required fields
		private DateTime    deliveryDateTime;

		private String      districtInfo;

		private Decimal     amount;

		private int         orderId;

		private int         itemId;

		private short       orderLineNumber;

		private short       quantity;

		private sbyte       districtId;

		private short       warehouseId;

		private short       supplyWarehouseId;

		private Company     company;

		private Warehouse   warehousePtr;

		private Stock       stockPtr;

		private String      itemName;

		private Decimal     itemPrice;

		private int         stockQuantity;

		private char        BrandGeneric;

		public Orderline(Company inCompany, int inOrderId, sbyte inDistrictId,
			short inWarehouseId, short inLineNumber,
			short number_of_orderlines, bool rollback) 
		{
			company = inCompany;
			orderId = inOrderId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
			orderLineNumber = inLineNumber;
			itemId = JBButil.create_random_item_id(company.getMaxItems(),
				warehouseId);
			if ((inLineNumber == number_of_orderlines) && rollback) 
			{
				itemId = 0;
			}
			//deliveryDateTime = null;//MPH. ValueType. can;t assign null. Check.this.
			quantity = 5;
			//Check this later,
			amount = Decimal.Zero ;//BigDecimal.valueOf(0, 2);
			districtInfo = null;
		}
        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public bool validateAndProcess(Warehouse inWarehousePtr)
        {
            lock (_syncRoot)
            {
                bool temp;
                Item itemRef = null;
                Stock stockRef = null;
                if (inWarehousePtr != null)
                {
                    itemRef = inWarehousePtr.retrieveItem(itemId);
                    stockRef = inWarehousePtr.retrieveStock(itemId);
                }
                if ((inWarehousePtr != null) && (itemRef != null) && (stockRef != null))
                {
                    stockPtr = stockRef;
                    process(itemRef, stockRef);
                    temp = true;
                }
                else
                    temp = false;
                return temp;
            }
        }
		private void process(Item itemRef, Stock stockRef) 
		{
			itemName = itemRef.getName();
			itemPrice = itemRef.getPrice();
			String itemData = itemRef.getBrandInfo();
			stockQuantity = stockRef.getQuantity();
			String stock_districtInfo = stockRef.getDistrictInfo(districtId);
			String stockData = stockRef.getData();
			if (stockQuantity >= (quantity + 10))
				stockRef.changeQuantity(-quantity);
			else
				stockRef.changeQuantity(91 - quantity);
			stockRef.incrementYTD(quantity);
			stockRef.incrementOrderCount();
			if (warehouseId == supplyWarehouseId)
				stockRef.incrementRemoteCount();
			//amount = BigDecimal.valueOf(quantity).multiply(itemPrice).setScale(2,BigDecimal.ROUND_HALF_UP);
			amount = Math.Round(Decimal.Multiply(quantity, itemPrice),2) ;
			if ((itemData.IndexOf("ORIGINAL") > 0)
				&& (stockData.IndexOf("ORIGINAL") > 0))
				BrandGeneric = 'B';
			else
				BrandGeneric = 'G';
            // CORECLR districtInfo = String.Copy(stock_districtInfo);
            districtInfo = stock_districtInfo;
        }


		public Stock getStockPtr() 
		{
			return stockPtr;
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public Decimal getAmount() 
		{
            lock (_syncRoot)
            {
                Decimal temp = amount;
                return temp;
            }
		}

		// CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
		public char getBrandGeneric() 
		{
            lock (_syncRoot)
            {
                char temp = BrandGeneric;
                return temp;
            }				
		}

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int getItemId()
        {
            lock (_syncRoot)
            {
                int temp = itemId;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public String getItemName()
        {
            lock (_syncRoot)
            {
                String temp = itemName;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public Decimal getItemPrice()
        {
            lock (_syncRoot)
            {
                Decimal temp = itemPrice;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public short getSupplyWarehouse()
        {
            lock (_syncRoot)
            {
                short temp = supplyWarehouseId;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public int getStockQuantity()
        {
            lock (_syncRoot)
            {
                int temp = stockQuantity;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setSupplyWarehouse(short inSupplyWarehouseId)
        {
            lock (_syncRoot)
            {
                supplyWarehouseId = inSupplyWarehouseId;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public DateTime getDeliveryDateTime()
        {
            lock (_syncRoot)
            {
                DateTime temp = deliveryDateTime;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setDeliveryTime(DateTime deliveryTime)
        {
            lock (_syncRoot)
            {
                deliveryDateTime = deliveryTime;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public short getQuantity()
        {
            lock (_syncRoot)
            {
                short temp = quantity;
                return temp;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setQuantity(short inQuantity)
        {
            lock (_syncRoot)
            {
                quantity = inQuantity;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setAmount(Decimal inAmount)
        {
            lock (_syncRoot)
            {
                amount = inAmount;
            }
        }
        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public void setDistrictInfo(String inDistrictInfo)
        {
            lock (_syncRoot)
            {
                districtInfo = inDistrictInfo;
            }
        }
	}
}
