﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.42
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by xsd, Version=2.0.50727.42.
// 
namespace CodePlex.TfsLibrary.ClientEngine {
    using System.Xml.Serialization;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "2.0.50727.42")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.codeplex.com/schema/IgnoreListSchema-1.0.xsd")]
    [System.Xml.Serialization.XmlRootAttribute("ignore", Namespace="http://www.codeplex.com/schema/IgnoreListSchema-1.0.xsd", IsNullable=false)]
    public partial class IgnoreElement {
        
        private string[] deleteField;
        
        private IgnoreAddElement[] addField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("delete")]
        public string[] delete {
            get {
                return this.deleteField;
            }
            set {
                this.deleteField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("add")]
        public IgnoreAddElement[] add {
            get {
                return this.addField;
            }
            set {
                this.addField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "2.0.50727.42")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.codeplex.com/schema/IgnoreListSchema-1.0.xsd")]
    public partial class IgnoreAddElement {
        
        private bool recursiveField;
        
        private string valueField;
        
        public IgnoreAddElement() {
            this.recursiveField = false;
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        [System.ComponentModel.DefaultValueAttribute(false)]
        public bool recursive {
            get {
                return this.recursiveField;
            }
            set {
                this.recursiveField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value {
            get {
                return this.valueField;
            }
            set {
                this.valueField = value;
            }
        }
    }
}