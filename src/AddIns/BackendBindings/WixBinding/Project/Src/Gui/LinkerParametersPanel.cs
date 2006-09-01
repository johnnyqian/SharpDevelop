﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Windows.Forms;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Internal.ExternalTool;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Gui.OptionPanels;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.WixBinding
{
	public class LinkerParametersPanel : AbstractProjectOptionPanel
	{
		public override void LoadPanelContents()
		{
			SetupFromXmlStream(this.GetType().Assembly.GetManifestResourceStream("ICSharpCode.WixBinding.Resources.LinkerParametersPanel.xfrm"));
			InitializeHelper();
			
			// Add the extension picker in manually since the anchoring does not
			// work if we add the picker into the XML of the LinkerParametersPanel.xfrm file.
			WixCompilerExtensionPicker extensionPicker = new WixCompilerExtensionPicker();
			extensionPicker.Dock = DockStyle.Fill;
			ControlDictionary["compilerExtensionsGroupBox"].Controls.Add(extensionPicker);
			extensionPicker.ExtensionsChanged += CompilerExtensionsChanged;
			
			WixCompilerExtensionBinding b = new WixCompilerExtensionBinding(extensionPicker);
			helper.AddBinding("LinkExtension", b);
		
			helper.AddConfigurationSelector(this);
		}
		
		void CompilerExtensionsChanged(object source, EventArgs e)
		{
			IsDirty = true;
		}
	}
}
