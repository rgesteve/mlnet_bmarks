/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for NewOrder.
	/// </summary>
	public class NewOrder
	{
		// This goes right after each class/interface statement
		//  static readonly String COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		private Order       orderPtr;

		private int         orderId;                                                    // NO_O_ID

		private sbyte       districtId;                                                 // NO_D_ID

		private short       warehouseId;                                                // NO_W_ID

		public void destroy() 
		{
		}

		public NewOrder(Order inOrderPtr, int inOrderId, sbyte inDistrictId,
			short inWarehouseId) 
		{
			orderPtr = inOrderPtr;
			orderId = inOrderId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
		}

                //09-04-07 Li: need to add this copy constructor to implement 
                //public NewOrder removeNewOrder(Object key) function in District.cs
                //please see relevant comment on District.cs
                public NewOrder(NewOrder inOrder)
                {
                       orderId = inOrder.orderId;
                       orderPtr = inOrder.orderPtr;
                       districtId = inOrder.districtId;
                       warehouseId = inOrder.warehouseId;
                }

		public void initNewOrder(Order inOrderPtr, int inOrderId,
			sbyte inDistrictId, short inWarehouseId) 
		{
			orderPtr = inOrderPtr;
			orderId = inOrderId;
			districtId = inDistrictId;
			warehouseId = inWarehouseId;
			//if (JBButil.getLog().isLoggable(Level.FINEST)) 
			if(JBButil.getLog().Level >= TraceLevel.Verbose)
			{
				Trace.WriteLineIf(JBButil.getLog().TraceVerbose,
					"NewOrder::initNewOrder, orderId=" + orderId
					+ ", districtId=" + districtId + ", warehouseId="
					+ warehouseId);
			}
		}

        private readonly object _syncRoot = new Object(); // CORECLR

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
		public Order getOrderPtr() 
		{
            lock (_syncRoot)
            {
                Order temp = orderPtr;
			return temp;
		}
}
	}
}
