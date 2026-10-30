// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace HorizonSync
{
    // Remember source labels so repeated language changes never translate user input.
    public sealed class UiLocalization
    {
        class Entry { public DependencyObject Target; public DependencyProperty Property; public string Source; }
        readonly List<Entry> entries=new List<Entry>();
        readonly HashSet<string> dynamicNames=new HashSet<string>{"LocationInfo","PreviewText","ClockText","StatusText","EngineStatusTitle","EngineStatusDetail","StartupInfo"};
        public UiLocalization(DependencyObject root){Capture(root);}
        void Remember(DependencyObject target,DependencyProperty property)
        {
            var value=target.GetValue(property) as string;
            if(!String.IsNullOrEmpty(value))entries.Add(new Entry{Target=target,Property=property,Source=value});
        }
        void Capture(DependencyObject node)
        {
            var element=node as FrameworkElement;
            bool dynamic=element!=null&&dynamicNames.Contains(element.Name);
            if(node is TextBlock&&!dynamic)Remember(node,TextBlock.TextProperty);
            if(node is ContentControl)Remember(node,ContentControl.ContentProperty);
            if(element!=null){Remember(node,FrameworkElement.ToolTipProperty);Remember(node,AutomationProperties.NameProperty);}
            foreach(object child in LogicalTreeHelper.GetChildren(node))
            {var dependency=child as DependencyObject;if(dependency!=null)Capture(dependency);}
        }
        public void Apply(){foreach(var e in entries)e.Target.SetValue(e.Property,Localization.T(e.Source));}
    }
}
