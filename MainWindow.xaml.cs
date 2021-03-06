﻿extern alias dstu2;
extern alias stu3;

using Hl7.ElementModel;
using dstu2.Hl7.Fhir.FluentPath;
using dstu2.Hl7.Fhir.Model;
using Hl7.FluentPath;
using Hl7.FluentPath.Expressions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
// using static Hl7.FluentPath.PathExpression;

namespace FhirPathTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public int TextControlFontSize { get; set; }

        public MainWindow()
        {
            TextControlFontSize = 22;
            InitializeComponent();
            textboxInputXML.Text = "<Patient xmlns=\"http://hl7.org/fhir\">\r\n<name>\r\n</name>\r\n<birthDate value=\"1973\"/>\r\n</Patient>";
            textboxExpression.Text = "birthDate < today()";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private IValueProvider GetResourceNavigator()
        {
            DomainResource resource = null;
            try
            {
                if (textboxInputXML.Text.StartsWith("{"))
                    resource = new dstu2.Hl7.Fhir.Serialization.FhirJsonParser().Parse<DomainResource>(textboxInputXML.Text);
                else
                    resource = new dstu2.Hl7.Fhir.Serialization.FhirXmlParser().Parse<DomainResource>(textboxInputXML.Text);
            }
            catch (Exception ex)
            {
                textboxResult.Text = "Resource read error:\r\n" + ex.Message;
                return null;
            }
            var inputNav = new PocoNavigator(resource);
            return inputNav;
        }

        private void ButtonGo_Click(object sender, RoutedEventArgs e)
        {
            var inputNav = GetResourceNavigator();
            if (inputNav == null)
                return;

            // Don't need to cache this, it is cached in the fhir-client
            CompiledExpression xps = null;
            try
            {
                xps = _compiler.Compile(textboxExpression.Text);
            }
            catch (Exception ex)
            {
                textboxResult.Text = "Expression compilation error:\r\n" + ex.Message;
                return;
            }

            IEnumerable<IValueProvider> prepopulatedValues = null;
            if (xps != null)
            {
                try
                {
                    prepopulatedValues = xps(inputNav, inputNav);
                }
                catch (Exception ex)
                {
                    textboxResult.Text = "Expression evaluation error:\r\n" + ex.Message;
                    AppendParseTree();
                    return;
                }

                textboxResult.Text = null;
                try
                {
                    if (prepopulatedValues.Count() > 0)
                    {
                        foreach (var t2 in prepopulatedValues.ToFhirValues())
                        {
                            if (t2 != null)
                            {
                                // output the content as XML fragments
                                var fragment = dstu2.Hl7.Fhir.Serialization.FhirSerializer.SerializeToXml(t2);
                                textboxResult.Text += fragment.Replace(" xmlns=\"http://hl7.org/fhir\"", "") + "\r\n";
                            }
                            // System.Diagnostics.Trace.WriteLine(string.Format("{0}: {1}", xpath.Value, t2.AsStringRepresentation()));
                        }
                    }
                }
                catch (Exception ex)
                {
                    textboxResult.Text = "Processing results error:\r\n" + ex.Message;
                    return;
                }
            }

            AppendParseTree();
        }

        private void AppendParseTree()
        {
            // Grab the parse expression
            StringBuilder sb = new StringBuilder();
            var expr = _compiler.Parse(textboxExpression.Text);
            OutputExpression(expr, sb, "");
            // textboxResult.Text += expr.Dump();
            textboxResult.Text += "\r\n\r\n----------------\r\n" + sb.ToString();
        }

        private void OutputExpression(Hl7.FluentPath.Expressions.Expression expr, StringBuilder sb, string prefix)
        {
            if (expr is ChildExpression)
            {
                var func = expr as ChildExpression;
                OutputExpression(func.Focus, sb, prefix + "-- ");
                sb.AppendFormat("{0}{1}\r\n", prefix, func.ChildName);
                return;
            }
            if (expr is FunctionCallExpression)
            {
                var func = expr as FunctionCallExpression;
                sb.AppendFormat("{0}{1}\r\n", prefix, func.FunctionName);
                OutputExpression(func.Focus, sb, prefix + "-- ");
                foreach (var item in func.Arguments)
                {
                    OutputExpression(item, sb, prefix + "    ");
                }
                return;
            }
            //else if (expr is BinaryExpression)
            //{
            //    var func = expr as BinaryExpression;
            //    sb.AppendLine(func.FunctionName);
            //    OutputExpression(func.Left, sb);
            //    sb.AppendLine(func.Op);
            //    OutputExpression(func.Right, sb);
            //    return;
            //}
            else if (expr is ConstantExpression)
            {
                var func = expr as ConstantExpression;
                sb.AppendFormat("{0}{1} (constant)\r\n", prefix, func.Value.ToString());
                return;
            }
            else if (expr is VariableRefExpression)
            {
                var func = expr as VariableRefExpression;
                // sb.AppendFormat("{0}{1} (variable ref)\r\n", prefix, func.Name);
                return;
            }
            sb.Append(expr.GetType().ToString());
        }

        Hl7.FluentPath.FluentPathCompiler _compiler = new FluentPathCompiler(CustomFluentPathFunctions.Scope);

        private void ButtonPredicate_Click(object sender, RoutedEventArgs e)
        {
            var inputNav = GetResourceNavigator();
            if (inputNav == null)
                return;

            // Don't need to cache this, it is cached in the fhir-client
            Hl7.FluentPath.CompiledExpression xps = null;
            try
            {
                xps = _compiler.Compile(textboxExpression.Text);
            }
            catch (Exception ex)
            {
                textboxResult.Text = "Expression compilation error:\r\n" + ex.Message;
                return;
            }

            if (xps != null)
            {
                try
                {
                    var result = xps.Predicate(inputNav, inputNav);
                    textboxResult.Text = result.ToString();
                }
                catch (Exception ex)
                {
                    textboxResult.Text = "Expression evaluation error:\r\n" + ex.Message;
                    return;
                }
            }

            AppendParseTree();
        }

        private void textboxExpression_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 100 || e.Delta < -100)
            {
                TextControlFontSize += e.Delta / 100;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextControlFontSize)));
            }
        }

        private void textboxInputXML_Drop(object sender, DragEventArgs e)
        {
            // This is the place where we want to support the reading of the file from the file system
            // to make the testing of other instances really easy
            var formats = e.Data.GetFormats();
            if (e.Data.GetDataPresent("FileName"))
            {
                string[] contents = e.Data.GetData("FileName") as string[];
                if (contents.Length > 0)
                    textboxInputXML.Text = System.IO.File.ReadAllText(contents[0]);
                e.Handled = true;
            }
        }

        private void textboxInputXML_DragOver(object sender, DragEventArgs e)
        {
            // e.AllowedEffects = DragDropEffects.Copy;
            e.Effects = DragDropEffects.Copy;
        }

        private void textboxInputXML_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}
