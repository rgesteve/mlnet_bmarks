/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Reflection;
using System.Diagnostics;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for Transaction.
	/// </summary>
	public abstract class Transaction
	{
		// This goes right after each class/interface statement
		//  static readonly String         COPYRIGHT          = "SPECjbb2005,"
		//  + "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		//  + "All rights reserved,"
		//  + "(C) Copyright IBM Corp., 1996 - 2005"
		//  + "All rights reserved,"
		//  + "Licensed Materials - Property of SPEC";

		public static readonly sbyte new_order          = 0; //protected to public due to difference with Java. Same as in specjbb1.0

		public static readonly sbyte payment            = 1;

		public static readonly sbyte order_status       = 2;

		public static readonly sbyte delivery           = 3;

		public static readonly sbyte stock_level        = 4;

		public static readonly sbyte cust_report        = 5;

		// sjm092398
		public static int           aveOrderlines      = 10;

		public static int           minOrderlines      = aveOrderlines - 5;

		public static int           maxOrderlines      = aveOrderlines + 5;

		public static bool          enableLogWrite     = false;

		public static bool          steadyStateMem     = true;

		public static bool                 validationFlag     = false;

		private static bool         validRun           = true;

        public static String[] transactionNames = {
            "NewOrder", "Payment", "OrderStatus", "Delivery", "StockLevel",
            "CustomerReport"};

        public enum TransactionTypes
        {
            NewOrderTransaction,
            PaymentTransaction,
            OrderStatusTransaction,
            DeliveryTransaction,
            StockLevelTransaction,
            CustomerReportTransaction
        }

        public/*protected*/ static readonly sbyte maxTxnTypes        = (sbyte) transactionNames.Length;

        public static Type[] transactionClasses;

        static Transaction() 
		{
			transactionClasses = new Type[Transaction.maxTxnTypes];
			for (int i = 0; i < maxTxnTypes; i++) 
			{
				try 
				{
					//transactionClasses[i] = Class.forName("spec.jbb."+ transactionNames[i] + "Transaction");
					transactionClasses[i] = Type.GetType("Specjbb2005.src.spec.jbb."+ transactionNames[i] + "Transaction");
				}
				catch (TypeLoadException e) 
				{
					Console.WriteLine("Transaction.transactionClasses - "+ "ClassNotFoundException " + e.Message );
				}
			}
		}

		public abstract String getMenuName();

		public abstract void init();

		public abstract bool process();

		public abstract void initializeTransactionLog();

		public abstract void processTransactionLog();

		public static void invalidateRun() 
		{
			validRun = false;
		}

		public static void validateRun() 
		{
			validRun = true;
		}

		public static bool isRunValid() 
		{
			return validRun;
		}

		public static void setOrderLineCount(int count) 
		{
			aveOrderlines = count;
			minOrderlines = aveOrderlines - 5;
			maxOrderlines = aveOrderlines + 5;
		}

		public static void setSteadyState(bool onoff) 
		{
			steadyStateMem = onoff;
		}

		public static void setLogWrite(bool onoff) 
		{
			enableLogWrite = onoff;
		}

		public static void setValidation(bool onoff) 
		{
			validationFlag = onoff;
		}

		//static Class[] transactionConstructorSignature = {
		//			Company.class, short.class,
		//			};
		
		static public Type[] transactionConstructorSignature = 
							{typeof(Company), typeof(short)};

        static public Transaction GetTransactionInstance(TransactionTypes transactionType, Company company, short warehouseID)
        {
            Transaction t = null;
            switch (transactionType)
            {
                case TransactionTypes.NewOrderTransaction:
                    return new Specjbb2005.src.spec.jbb.NewOrderTransaction(company, warehouseID);
                case TransactionTypes.PaymentTransaction:
                    return new Specjbb2005.src.spec.jbb.PaymentTransaction(company, warehouseID);
                case TransactionTypes.OrderStatusTransaction:
                    return new Specjbb2005.src.spec.jbb.OrderStatusTransaction(company, warehouseID);
                case TransactionTypes.DeliveryTransaction:
                    return new Specjbb2005.src.spec.jbb.DeliveryTransaction(company, warehouseID);
                case TransactionTypes.StockLevelTransaction:
                    return new Specjbb2005.src.spec.jbb.StockLevelTransaction(company, warehouseID);
                case TransactionTypes.CustomerReportTransaction:
                    return new Specjbb2005.src.spec.jbb.CustomerReportTransaction(company, warehouseID);
            }

            return t;
        }

        // CORECLR ImplGetTransactionInstance replaced this
        //
        //      static public  Transaction getInstance(Type transactionClass, Company company,
        //	short warehouseID) 
        //{
        //	Transaction t = null;
        //	try 
        //	{
        //              // CORECLR TODO Implement base class
        //              //
        //              //This might be slower. How about using Activator ?? TODO:Check.
        //              // CORECLR ConstructorInfo ctor = transactionClass.GetConstructor(transactionConstructorSignature);
        //              ConstructorInfo ctor = typeof(int).GetConstructor.GetConstructor(transactionConstructorSignature);

        //              //ConstructorInfo[] ctor = transactionClass.GetConstructors(transactionConstructorSignature);
        //              Object[] args = new Object[2];
        //		args[0] = company;
        //		args[1] = warehouseID;//new Short(warehouseID);
        //		t = (Transaction)ctor.Invoke(args);//(Transaction) ctor.newInstance(args);
        //	}
        //	catch (Exception e) 
        //	{
        //		Trace.WriteLineIf(JBButil.getLog().TraceWarning, "Exception: " + e.Message);
        //	}
        //	return t;
        //}

    }
}
