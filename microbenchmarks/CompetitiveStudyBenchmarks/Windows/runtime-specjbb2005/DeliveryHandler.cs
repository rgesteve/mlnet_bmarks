/*
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.IO;


namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for DeliveryHandler.
	/// </summary>
	public class DeliveryHandler
	{
		// This goes right after each class/interface statement
		//  internal static readonly String        COPYRIGHT = "SPECjbb2005,"
		//  + "Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		static private StreamWriter outFile;

		public DeliveryHandler(StreamWriter ps) 
		{
			lock (GetType())
			{
				outFile = ps;
			}
		}

		public void handleDelivery(DeliveryTransaction deliveryTransaction) 
		{
			// this needs to be asynchronous
			deliveryTransaction.preprocess();
			// send record of delivery to file
			deliveryTransaction.display(outFile);
		}

		public DeliveryHandler()
		{
			
		}
	}
}
