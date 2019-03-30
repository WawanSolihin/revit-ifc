//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using Revit.IFC.Common.Utility;
using Revit.RevitIFCTools.Extension;

namespace RevitIFCTools
{
   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class IFCEntityListWin: Window
   {
      SortedSet<string> aggregateEntities;
      string outputFolder = @"c:\temp";
      public IFCEntityListWin()
      {
         InitializeComponent();
         textBox_outputFolder.Text = outputFolder; // set default
         button_subtypeTest.IsEnabled = false;
         button_supertypeTest.IsEnabled = false;
         button_Go.IsEnabled = false;
      }

      private void button_browse_Click(object sender, RoutedEventArgs e)
      {
         var dialog = new FolderBrowserDialog();
         dialog.ShowDialog();
         textBox_folderLocation.Text = dialog.SelectedPath;
         if (string.IsNullOrEmpty(textBox_folderLocation.Text))
            return;

         DirectoryInfo dInfo = new DirectoryInfo(dialog.SelectedPath);
         foreach (FileInfo f in dInfo.GetFiles("IFC*.xsd"))
         {
            listBox_schemaList.Items.Add(f.Name);
         }
      }

      /// <summary>
      /// Procees an IFC schema from the IFCXML schema
      /// </summary>
      /// <param name="f">IFCXML schema file</param>
      private void processSchema(FileInfo f)
      {
         ProcessIFCXMLSchema.ProcessIFCSchema(f);

         string schemaName = f.Name.Replace(".xsd", "");

         if (checkBox_outputSchemaTree.IsChecked == true)
         {
            string treeDump = IfcSchemaEntityTree.DumpTree();
            System.IO.File.WriteAllText(outputFolder + @"\entityTree" + schemaName + ".txt", treeDump);
         }

         if (checkBox_outputSchemaEnum.IsChecked == true)
         {
            string dictDump = IfcSchemaEntityTree.DumpEntityDict(schemaName);
            System.IO.File.WriteAllText(outputFolder + @"\entityEnum" + schemaName + ".cs", dictDump);
         }

         // Add aggregate of the entity list into a set
         foreach (KeyValuePair<string,IfcSchemaEntityNode> entry in IfcSchemaEntityTree.EntityDict)
         {
            aggregateEntities.Add(entry.Key);
         }
      }

      private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         if (listBox_schemaList.SelectedItems.Count > 0)
            button_Go.IsEnabled = true;
         else
            button_Go.IsEnabled = false;
      }

      private void button_Go_Click(object sender, RoutedEventArgs e)
      {
         if (listBox_schemaList.SelectedItems.Count == 0)
            return;

         DirectoryInfo dInfo = new DirectoryInfo(textBox_folderLocation.Text);
         if (dInfo == null)
            return;

         if (aggregateEntities == null)
            aggregateEntities = new SortedSet<string>();
         aggregateEntities.Clear();

         ProcessPsetDefinition procPsetDef = new ProcessPsetDefinition();

         IDictionary<string, FXIFCEntityAndPsetList> fxEntityNPsetDict = new Dictionary<string, FXIFCEntityAndPsetList>();

         foreach (string fileName in listBox_schemaList.SelectedItems)
         {
            FileInfo f = dInfo.GetFiles(fileName).First();
            processSchema(f);

            // Add creation of Json file for FORNAX universal template
            string schemaName = f.Name.Replace(".xsd", "");
            IDictionary<string, IfcSchemaEntityNode> entDict = IfcSchemaEntityTree.GetEntityDictFor(schemaName);
            FXIFCEntityAndPsetList entPsetList = new FXIFCEntityAndPsetList();
            foreach (KeyValuePair<string, IfcSchemaEntityNode> ent in entDict)
            {
               FXEntityInfo entPdef = new FXEntityInfo();
               entPdef.Entity = ent.Key;
               if (!string.IsNullOrEmpty(ent.Value.PredefinedType))
               {
                  if (IfcSchemaEntityTree.PredefinedTypeEnumDict.ContainsKey(ent.Value.PredefinedType))
                  {
                     entPdef.PredefinedType = IfcSchemaEntityTree.PredefinedTypeEnumDict[ent.Value.PredefinedType];
                  }
               }
               entPsetList.EntityList.Add(entPdef);
            }

            DirectoryInfo[] psdFolders = new DirectoryInfo(System.IO.Path.Combine(textBox_folderLocation.Text, schemaName)).GetDirectories("psd", SearchOption.AllDirectories);
            FXPropertySetDef psetDefList = new FXPropertySetDef();
            foreach (DirectoryInfo subDir in psdFolders[0].GetDirectories())
            {
               foreach (FileInfo file in subDir.GetFiles("Pset_*.xml"))
               {
                  PropertySet.PsetDefinition psetD = ProcessPsetDefinition(schemaFolder, file);
                  AddPsetDefToDict(schemaFolder, psetD);
               }
               //psetDefList.PsetName =
         } }

         if (aggregateEntities.Count > 0)
         {
            string entityList;
            entityList = "using System;"
                        + "\nusing System.Collections.Generic;"
                        + "\nusing System.Linq;"
                        + "\nusing System.Text;"
                        + "\n"
                        + "\nnamespace Revit.IFC.Common.Enums"
                        + "\n{"
                        + "\n\t/// <summary>"
                        + "\n\t/// IFC entity types. Combining IFC2x3 and IFC4 (Add2) entities."
                        + "\n\t/// List of Entities for IFC2x is found in IFC2xEntityType.cs"
                        + "\n\t/// List of Entities for IFC4 is found in IFC4EntityType.cs"
                        + "\n\t/// </summary>"
                        + "\n\tpublic enum IFCEntityType"
                        + "\n\t{";

            foreach (string ent in aggregateEntities)
            {
               entityList += "\n\t\t/// <summary>"
                           + "\n\t\t/// IFC Entity " + ent + " enumeration"
                           + "\n\t\t/// </summary>"
                           + "\n\t\t" + ent + ",\n";
            }
            entityList += "\n\t\tUnknown,"
                        + "\n\t\tDontExport"
                        + "\n\t}"
                        + "\n}";
            System.IO.File.WriteAllText(outputFolder + @"\IFCEntityType.cs", entityList);
         }

         // Only allows test when only one schema is selected
         if (listBox_schemaList.SelectedItems.Count == 1)
         {
            button_subtypeTest.IsEnabled = true;
            button_supertypeTest.IsEnabled = true;
         }
         else
         {
            button_subtypeTest.IsEnabled = false;
            button_supertypeTest.IsEnabled = false;
         }
      }

      private void button_Cancel_Click(object sender, RoutedEventArgs e)
      {
         Close();
      }

      private void button_browseOutputFolder_Click(object sender, RoutedEventArgs e)
      {
         var dialog = new FolderBrowserDialog();
         dialog.ShowDialog();
         textBox_outputFolder.Text = dialog.SelectedPath;
         outputFolder = dialog.SelectedPath;
      }

      private void button_subtypeTest_Click(object sender, RoutedEventArgs e)
      {
         if (string.IsNullOrEmpty(textBox_type1.Text) || string.IsNullOrEmpty(textBox_type2.Text))
            return;

         bool res = IfcSchemaEntityTree.IsSubTypeOf(textBox_type1.Text, textBox_type2.Text);
         if (res)
            checkBox_testResult.IsChecked = true;
         else
            checkBox_testResult.IsChecked = false;
      }

      private void button_supertypeTest_Click(object sender, RoutedEventArgs e)
      {
         if (string.IsNullOrEmpty(textBox_type1.Text) || string.IsNullOrEmpty(textBox_type2.Text))
            return;

         bool res = IfcSchemaEntityTree.IsSuperTypeOf(textBox_type1.Text, textBox_type2.Text);
         if (res)
            checkBox_testResult.IsChecked = true;
         else
            checkBox_testResult.IsChecked = false;
      }

      private void textBox_type1_TextChanged(object sender, TextChangedEventArgs e)
      {
         checkBox_testResult.IsChecked = false;
      }

      private void textBox_type2_TextChanged(object sender, TextChangedEventArgs e)
      {
         checkBox_testResult.IsChecked = false;
      }

      private void textBox_outputFolder_TextChanged(object sender, TextChangedEventArgs e)
      {
         outputFolder = textBox_outputFolder.Text;
      }
   }
}
