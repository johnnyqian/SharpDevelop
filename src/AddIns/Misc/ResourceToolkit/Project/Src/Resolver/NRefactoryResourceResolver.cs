// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Christian Hornung" email=""/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Hornung.ResourceToolkit.ResourceFileContent;
using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Ast;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.TextEditor.Document;

namespace Hornung.ResourceToolkit.Resolver
{
	/// <summary>
	/// Resolves resource references using NRefactory.
	/// </summary>
	public class NRefactoryResourceResolver : AbstractResourceResolver
	{
		
		/// <summary>
		/// The AddIn tree path where the NRefactory resource resolvers are registered.
		/// </summary>
		public const string NRefactoryResourceResolversAddInTreePath = "/AddIns/ResourceToolkit/NRefactoryResourceResolver/Resolvers";
		
		// ********************************************************************************************************************************
		
		static List<INRefactoryResourceResolver> resolvers;
		
		/// <summary>
		/// Gets a list of all registered NRefactory resource resolvers.
		/// </summary>
		public static IEnumerable<INRefactoryResourceResolver> Resolvers {
			get {
				if (resolvers == null) {
					resolvers = AddInTree.BuildItems<INRefactoryResourceResolver>(NRefactoryResourceResolversAddInTreePath, null, false);
				}
				return resolvers;
			}
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Initializes a new instance of the <see cref="NRefactoryResourceResolver"/> class.
		/// </summary>
		public NRefactoryResourceResolver() : base()
		{
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Determines whether this resolver supports resolving resources in the given file.
		/// </summary>
		/// <param name="fileName">The name of the file to examine.</param>
		/// <returns><c>true</c>, if this resolver supports resolving resources in the given file, <c>false</c> otherwise.</returns>
		public override bool SupportsFile(string fileName)
		{
			// Any source code file supported by NRefactory is supported
			return (GetFileLanguage(fileName) != null);
		}
		
		/// <summary>
		/// Gets a list of patterns that can be searched for in the specified file
		/// to find possible resource references that are supported by this
		/// resolver.
		/// </summary>
		/// <param name="fileName">The name of the file to get a list of possible patterns for.</param>
		public override IEnumerable<string> GetPossiblePatternsForFile(string fileName)
		{
			if (this.SupportsFile(fileName)) {
				List<string> patterns = new List<string>();
				foreach (INRefactoryResourceResolver resolver in Resolvers) {
					foreach (string pattern in resolver.GetPossiblePatternsForFile(fileName)) {
						if (!patterns.Contains(pattern)) {
							patterns.Add(pattern);
						}
					}
				}
				return patterns;
			}
			return new string[0];
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Attempts to resolve a reference to a resource.
		/// </summary>
		/// <param name="fileName">The name of the file that contains the expression to be resolved.</param>
		/// <param name="document">The document that contains the expression to be resolved.</param>
		/// <param name="caretLine">The 0-based line in the file that contains the expression to be resolved.</param>
		/// <param name="caretColumn">The 0-based column position of the expression to be resolved.</param>
		/// <param name="caretOffset">The offset of the position of the expression to be resolved.</param>
		/// <param name="charTyped">The character that has been typed at the caret position but is not yet in the buffer (this is used when invoked from code completion), or <c>null</c>.</param>
		/// <returns>A <see cref="ResourceResolveResult"/> that describes which resource is referenced by the expression at the specified position in the specified file, or <c>null</c> if that expression does not reference a (known) resource.</returns>
		protected override ResourceResolveResult Resolve(string fileName, IDocument document, int caretLine, int caretColumn, int caretOffset, char? charTyped)
		{
			IExpressionFinder ef = ParserService.GetExpressionFinder(fileName);
			if (ef == null) {
				return null;
			}
			
			ExpressionResult result = ef.FindFullExpression(document.TextContent, caretOffset);
			
			if (result.Expression == null) {
				// may happen if in string
				while (--caretOffset > 0 && (result = ef.FindFullExpression(document.TextContent, caretOffset)).Expression == null) {
					if (document.GetLineNumberForOffset(caretOffset) != caretLine) {
						// only look in same line
						break;
					}
				}
			}
			
			if (result.Expression != null) {
				
				Expression expr = NRefactoryAstCacheService.ParseExpression(fileName, result.Expression);
				
				if (expr == null) {
					return null;
				}
				
				return TryResolve(result, expr, caretLine, caretColumn, fileName, document.TextContent, ef, charTyped);
				
			}
			
			return null;
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Tries to resolve the resource reference using all available
		/// NRefactory resource resolvers.
		/// </summary>
		static ResourceResolveResult TryResolve(ExpressionResult result, Expression expr, int caretLine, int caretColumn, string fileName, string fileContent, IExpressionFinder expressionFinder, char? charTyped)
		{
			ResolveResult rr = NRefactoryAstCacheService.ResolveLowLevel(fileName, caretLine+1, caretColumn+1, null, result.Expression, expr, result.Context);
			if (rr != null) {
				
				ResourceResolveResult rrr;
				foreach (INRefactoryResourceResolver resolver in Resolvers) {
					if ((rrr = resolver.Resolve(result, expr, rr, caretLine, caretColumn, fileName, fileContent, expressionFinder, charTyped)) != null) {
						return rrr;
					}
				}
				
			}
			
			return null;
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Determines the file which contains the resources referenced by the specified manifest resource name.
		/// </summary>
		/// <param name="sourceFileName">The name of the source code file which the reference occurs in.</param>
		/// <param name="resourceName">The manifest resource name to find the resource file for.</param>
		/// <returns>A <see cref="ResourceSetReference"/> with the specified resource set name and the name of the file that contains the resources with the specified manifest resource name, or <c>null</c> if the file name cannot be determined.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="resourceName"/> parameter is <c>null</c>.</exception>
		public static ResourceSetReference GetResourceSetReference(string sourceFileName, string resourceName)
		{
			if (resourceName == null) {
				throw new ArgumentNullException("resourceName");
			}
			
			IProject p = ProjectFileDictionaryService.GetProjectForFile(sourceFileName);
			
			if (p != null) {
				
				string fileName = null;
				
				if (resourceName.StartsWith(p.RootNamespace, StringComparison.InvariantCultureIgnoreCase)) {
					
					if (p.Language == "VBNet") {
						
						// SD2-1239
						// The VB MSBuild tasks do not use the folder names 
						// in the manifest resource names.
						// We have to look in all folders of the project
						// for a file with the specified resource name.
						
						// Need to add a dummy extension so that FindResourceFileName
						// does not remove parts of the actual file name when it
						// contains a dot.
						string fileNameWithDummyExtension = String.Concat(resourceName.Substring(p.RootNamespace.Length+1), ".x");
						
						// Search in the project root folder
						// (this folder is not specified explicitly in
						// the MSBuild project)
						if ((fileName = FindResourceFileName(Path.Combine(p.Directory, fileNameWithDummyExtension))) != null) {
							return new ResourceSetReference(resourceName, fileName);
						}
						
						// Search in all project folders
						foreach (ProjectItem folder in p.GetItemsOfType(ItemType.Folder)) {
							if ((fileName = FindResourceFileName(Path.Combine(folder.FileName, fileNameWithDummyExtension))) != null) {
								return new ResourceSetReference(resourceName, fileName);
							}
						}
						
					} else {
						
						// Look for a resource file in the project with the exact name.
						if ((fileName = FindResourceFileName(Path.Combine(p.Directory, resourceName.Substring(p.RootNamespace.Length+1).Replace('.', Path.DirectorySeparatorChar)))) != null) {
							return new ResourceSetReference(resourceName, fileName);
						}
						
					}
					
				}
				
				// SharpDevelop silently strips the (hard-coded) folder names
				// "src" and "source" when generating the default namespace name
				// for new files.
				// When MSBuild generates the manifest resource names for the
				// forms designer resources, it uses the type name of the
				// first class in the file. So we should find all files
				// that contain a type with the name in resourceName
				// and then look for dependent resource files or resource files
				// with the same name in the same directory as the source files.
				
				// Find all source files that contain a type with the same
				// name as the resource we are looking for.
				List<string> possibleSourceFiles = new List<string>();
				IProjectContent pc = ParserService.GetProjectContent(p);
				if (pc != null) {
					
					IClass resourceClass = pc.GetClass(resourceName, 0);
					
					if (resourceClass != null) {
						CompoundClass cc = resourceClass.GetCompoundClass() as CompoundClass;
						
						foreach (IClass c in (cc == null ? new IClass[] { resourceClass } : cc.GetParts())) {
							if (c.CompilationUnit != null && c.CompilationUnit.FileName != null) {
								
								#if DEBUG
								LoggingService.Debug("ResourceToolkit: NRefactoryResourceResolver found file '"+c.CompilationUnit.FileName+"' to contain the type '"+resourceName+"'");
								#endif
								
								possibleSourceFiles.Add(c.CompilationUnit.FileName);
								
							}
						}
						
					}
					
				}
				
				foreach (string possibleSourceFile in possibleSourceFiles) {
					string possibleSourceFileName = Path.GetFileName(possibleSourceFile);
					
					// Find resource files dependent on these source files.
					foreach (ProjectItem pi in p.Items) {
						FileProjectItem fpi = pi as FileProjectItem;
						if (fpi != null) {
							if (fpi.DependentUpon != null &&
							    (fpi.ItemType == ItemType.EmbeddedResource || fpi.ItemType == ItemType.Resource || fpi.ItemType == ItemType.None) &&
							    FileUtility.IsEqualFileName(fpi.DependentUpon, possibleSourceFileName)) {
								
								#if DEBUG
								LoggingService.Debug("ResourceToolkit: NRefactoryResourceResolver trying to use dependent file '"+fpi.FileName+"' as resource file");
								#endif
								
								if ((fileName = FindResourceFileName(fpi.FileName)) != null) {
									// Prefer culture-invariant resource file
									// over localized resource file
									IResourceFileContent rfc = ResourceFileContentRegistry.GetResourceFileContent(fileName);
									if (rfc.Culture.Equals(CultureInfo.InvariantCulture)) {
										return new ResourceSetReference(resourceName, fileName);
									}
								}
								
							}
						}
					}
					
					// Fall back to any found resource file
					// if no culture-invariant resource file was found
					if (fileName != null) {
						return new ResourceSetReference(resourceName, fileName);
					}
					
					// Find resource files with the same name as the source file
					// and in the same directory.
					if ((fileName = FindResourceFileName(possibleSourceFile)) != null) {
						return new ResourceSetReference(resourceName, fileName);
					}
					
				}
				
			} else {
				
				#if DEBUG
				LoggingService.Info("ResourceToolkit: NRefactoryResourceResolver.GetResourceSetReference could not determine the project for the source file '"+(sourceFileName ?? "<null>")+"'.");
				#endif
				
				if (sourceFileName != null) {
					
					// The project could not be determined.
					// Try a simple file search.
					
					string directory = Path.GetDirectoryName(sourceFileName);
					string resourcePart = resourceName;
					string fileName;
					
					while (true) {
						
						#if DEBUG
						LoggingService.Debug("ResourceToolkit: NRefactoryResourceResolver.GetResourceSetReference: looking for a resource file like '"+Path.Combine(directory, resourcePart)+"'");
						#endif
						
						if ((fileName = FindResourceFileName(Path.Combine(directory, resourcePart.Replace('.', Path.DirectorySeparatorChar)))) != null) {
							return new ResourceSetReference(resourceName, fileName);
						}
						if ((fileName = FindResourceFileName(Path.Combine(directory, resourcePart))) != null) {
							return new ResourceSetReference(resourceName, fileName);
						}
						
						if (resourcePart.Contains(".")) {
							resourcePart = resourcePart.Substring(resourcePart.IndexOf('.')+1);
						} else {
							break;
						}
						
					}
					
				}
				
			}
			
			#if DEBUG
			LoggingService.Info("ResourceToolkit: NRefactoryResourceResolver.GetResourceSetReference is unable to find a suitable resource file for '"+resourceName+"'");
			#endif
			
			return new ResourceSetReference(resourceName, null);
		}
		
		// ********************************************************************************************************************************
		
		/// <summary>
		/// Gets the NRefactory language for the specified file name.
		/// </summary>
		public static SupportedLanguage? GetFileLanguage(string fileName)
		{
			string ext = Path.GetExtension(fileName);
			if (ext.Equals(".cs", StringComparison.InvariantCultureIgnoreCase))
				return SupportedLanguage.CSharp;
			if (ext.Equals(".vb", StringComparison.InvariantCultureIgnoreCase))
				return SupportedLanguage.VBNet;
			return null;
		}
		
		/// <summary>
		/// Gets the language properties for the project the specified member
		/// belongs to.
		/// Returns <c>null</c> if the language cannot be determined.
		/// </summary>
		public static LanguageProperties GetLanguagePropertiesForMember(IMember member)
		{
			if (member == null) {
				return null;
			}
			if (member.DeclaringType == null) {
				return null;
			}
			if (member.DeclaringType.CompilationUnit == null) {
				return null;
			}
			if (member.DeclaringType.CompilationUnit.ProjectContent == null) {
				return null;
			}
			return member.DeclaringType.CompilationUnit.ProjectContent.Language;
		}
		
		/// <summary>
		/// Gets the language properties for the specified file.
		/// </summary>
		/// <param name="fileName">The file to get the language properties for.</param>
		/// <returns>The language properties of the specified file, or <c>null</c> if the language cannot be determined.</returns>
		public static LanguageProperties GetLanguagePropertiesForFile(string fileName)
		{
			ICSharpCode.SharpDevelop.Dom.IParser p = ParserService.GetParser(fileName);
			if (p == null) {
				return null;
			}
			return p.Language;
		}
		
	}
}
