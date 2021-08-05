/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for JBBSortedStorage.
	/// </summary>
	public interface JBBSortedStorage : JBBDataStorage
	{
		/*
		// This goes right after each class/interface statement
		static readonly String COPYRIGHT = "SPECjbb2005,"
		+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "(C) Copyright IBM Corp., 1996 - 2005"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";
		*/
		/**
		 * Removes the first element from JBBDataStorage
		 */
		Object removeFirstElem();

		/**
		 * Removes the first "quant" elements from JBBDataStorage and deletes an
		 * Entities associated with their values
		 */
		bool deleteFirstEntities(int quant);

		/**
		 * Removes the first element from JBBDataStorage and deletes an Entity
		 * associated with its value
		 */
		bool deleteFirstEntities();

		/**
		 * Returns the median element of the sub-set of JBBDataStorage
		 */
		Object getMedianValue(Object firstKey, Object lastKey);

        void setKeyListToNull(); //03/07/2007 Milind
		
	}
}
