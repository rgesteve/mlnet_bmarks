/*
 * 
 * Copyright (c) 2000-2005 Standard Performance Evaluation Corporation (SPEC) All
 * rights reserved. Copyright (c) 1996-2005 IBM Corporation, Inc. All rights
 * reserved.
 */
using System;
using System.Collections;

namespace Specjbb2005.src.spec.jbb
{
	/// <summary>
	/// Summary description for JBBDataStorage.
	/// </summary>
	public interface JBBDataStorage
	{
		// This goes right after each class/interface statement
		/*
		static readonly String COPYRIGHT = "SPECjbb2005,"
		+ "Copyright (c) 2005 Standard Performance Evaluation Corporation (SPEC),"
		+ "All rights reserved,"
		+ "(C) Copyright IBM Corp., 1996 - 2005"
		+ "All rights reserved,"
		+ "Licensed Materials - Property of SPEC";
		*/
		/**
		 * Returns true if JBBDataStorage contains a value for specified key
		 */
		bool containsKey(Object key);

		/**
		 * Returns the value for specified key
		 */
		Object get(Object key);

		/**
		 * Associates the specified value with specified key
		 */
		Object put(Object key, Object value1);

		/**
		 * Removes the value for the specified key
		 */
		Object remove(Object key);

		/**
		 * Returns the number of key-value pairs in the JBBDataStorage
		 */
		int size();

		/**
		 * Returns an iterator over the elements in this JBBDataStorage
		 */
		IEnumerator elements();
	}
}
