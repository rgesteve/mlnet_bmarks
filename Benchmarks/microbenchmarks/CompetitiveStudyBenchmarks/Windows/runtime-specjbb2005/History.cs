/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 * 
 */
using System;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for History.
	/// </summary>
	public class History
	{
		// This goes right after each class/interface statement
		//  internal static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private DateTime    date;

		private String      data;

		private Decimal     amount;

		private short       customerId;

		private sbyte       customerDistrictId;

		private short       customerWarehouseId;

		private sbyte       districtId;

		private short       warehouseId;

		public History(short inCustomerId, sbyte inCustomerDistrictId,
			short inCustomerWarehouseId, sbyte inDistrictId,
			short inWarehouseId, DateTime inDate, Decimal inAmount, String inData) 
		{
			customerId = inCustomerId;
			customerDistrictId = inCustomerDistrictId;
			customerWarehouseId = inCustomerWarehouseId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
			date = inDate;
			amount = inAmount;
			data = inData;
		}

		public History() 
		{
		}

		public void initHistory(short inCustomerId, sbyte inCustomerDistrictId,
			short inCustomerWarehouseId, sbyte inDistrictId,
			short inWarehouseId, DateTime inDate, Decimal inAmount, String inData) 
		{
			customerId = inCustomerId;
			customerDistrictId = inCustomerDistrictId;
			customerWarehouseId = inCustomerWarehouseId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
			date = inDate;
			amount = inAmount;
			data = inData;
		}

        private readonly object _syncRoot = new Object(); // CORECLR

        // CORECLR [MethodImplAttribute(MethodImplOptions.Synchronized)]
        public String buildData(String warehouseName,
                                       String districtName)
        {
            lock (_syncRoot)

            {            // CORECLR data = String.Copy((warehouseName + "   " + districtName)) ;//new String((warehouseName + "   " + districtName));
                data = warehouseName + "   " + districtName;
                String temp = data;
                return temp;
            }
        }
		public short getCustomerId() 
		{
			short temp = customerId;
			return temp;
		}

		public DateTime getDate() 
		{
			DateTime temp = date;
			return temp;
		}

		public Decimal getAmount() 
		{
			Decimal temp = amount;
			return temp;
		}
	}
}
