﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using ICSharpCode.PythonBinding;
using ICSharpCode.Scripting.Tests.Utils;
using ICSharpCode.SharpDevelop.Dom;
using NUnit.Framework;
using PythonBinding.Tests.Utils;

namespace PythonBinding.Tests.Completion
{
	[TestFixture]
	public class FromImportPythonModuleCompletionTests
	{
		PythonImportCompletion completion;
		MockProjectContent projectContent;
		
		[SetUp]
		public void Init()
		{
			projectContent = new MockProjectContent();
			ParseInformation parseInfo = new ParseInformation(new DefaultCompilationUnit(projectContent));
			completion = new PythonImportCompletion(projectContent);
		}
		
		[Test]
		public void GetCompletionItemsFromModule_UnknownModule_OnlyImportAllItemsCompletionItemReturned()
		{
			List<ICompletionEntry> items = completion.GetCompletionItemsFromModule("unknown");
			List<ICompletionEntry> expectedItems = new List<ICompletionEntry>();
			expectedItems.Add(new NamespaceEntry("*"));
			Assert.AreEqual(expectedItems, items);
		}
		
		[Test]
		public void GetCompletionItemsFromModule_MathModule_ReturnsPiField()
		{
			List<ICompletionEntry> items = completion.GetCompletionItemsFromModule("math");
			IField field = PythonCompletionItemsHelper.FindFieldFromCollection("pi", items);
			Assert.IsNotNull(field);
		}
		
		[Test]
		public void GetCompletionItemsFromModule_MathModule_LastCompletionItemIsAsterisk()
		{
			List<ICompletionEntry> items = completion.GetCompletionItemsFromModule("math");
			int lastItem = items.Count - 1;			
			ICompletionEntry lastCompletionItem = items[lastItem];
			NamespaceEntry expectedCompletionItem = new NamespaceEntry("*");
			Assert.AreEqual(expectedCompletionItem, lastCompletionItem);
		}
	}
}
