﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.1434
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// 
// This source code was auto-generated by Microsoft.VSDesigner, Version 2.0.50727.1434.
// 
#pragma warning disable 1591

namespace CodePlex.TfsLibrary.RegistrationWebSvc {
    using System.Diagnostics;
    using System.Web.Services;
    using System.ComponentModel;
    using System.Web.Services.Protocols;
    using System;
    using System.Xml.Serialization;
    
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "2.0.50727.1434")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Web.Services.WebServiceBindingAttribute(Name="RegistrationSoap", Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class Registration : System.Web.Services.Protocols.SoapHttpClientProtocol {
        
        private System.Threading.SendOrPostCallback GetRegistrationEntriesOperationCompleted;
        
        private bool useDefaultCredentialsSetExplicitly;
        
        /// <remarks/>
        public Registration() {
            this.Url = global::CodePlex.TfsLibrary.Properties.Settings.Default.TfsObjectModel_RegistrationWebSvc_Registration;
            if ((this.IsLocalFileSystemWebService(this.Url) == true)) {
                this.UseDefaultCredentials = true;
                this.useDefaultCredentialsSetExplicitly = false;
            }
            else {
                this.useDefaultCredentialsSetExplicitly = true;
            }
        }
        
        public new string Url {
            get {
                return base.Url;
            }
            set {
                if ((((this.IsLocalFileSystemWebService(base.Url) == true) 
                            && (this.useDefaultCredentialsSetExplicitly == false)) 
                            && (this.IsLocalFileSystemWebService(value) == false))) {
                    base.UseDefaultCredentials = false;
                }
                base.Url = value;
            }
        }
        
        public new bool UseDefaultCredentials {
            get {
                return base.UseDefaultCredentials;
            }
            set {
                base.UseDefaultCredentials = value;
                this.useDefaultCredentialsSetExplicitly = true;
            }
        }
        
        /// <remarks/>
        public event GetRegistrationEntriesCompletedEventHandler GetRegistrationEntriesCompleted;
        
        /// <remarks/>
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03/GetR" +
            "egistrationEntries", RequestNamespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03", ResponseNamespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03", Use=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)]
        public RegistrationEntry[] GetRegistrationEntries(string toolId) {
            object[] results = this.Invoke("GetRegistrationEntries", new object[] {
                        toolId});
            return ((RegistrationEntry[])(results[0]));
        }
        
        /// <remarks/>
        public void GetRegistrationEntriesAsync(string toolId) {
            this.GetRegistrationEntriesAsync(toolId, null);
        }
        
        /// <remarks/>
        public void GetRegistrationEntriesAsync(string toolId, object userState) {
            if ((this.GetRegistrationEntriesOperationCompleted == null)) {
                this.GetRegistrationEntriesOperationCompleted = new System.Threading.SendOrPostCallback(this.OnGetRegistrationEntriesOperationCompleted);
            }
            this.InvokeAsync("GetRegistrationEntries", new object[] {
                        toolId}, this.GetRegistrationEntriesOperationCompleted, userState);
        }
        
        private void OnGetRegistrationEntriesOperationCompleted(object arg) {
            if ((this.GetRegistrationEntriesCompleted != null)) {
                System.Web.Services.Protocols.InvokeCompletedEventArgs invokeArgs = ((System.Web.Services.Protocols.InvokeCompletedEventArgs)(arg));
                this.GetRegistrationEntriesCompleted(this, new GetRegistrationEntriesCompletedEventArgs(invokeArgs.Results, invokeArgs.Error, invokeArgs.Cancelled, invokeArgs.UserState));
            }
        }
        
        /// <remarks/>
        public new void CancelAsync(object userState) {
            base.CancelAsync(userState);
        }
        
        private bool IsLocalFileSystemWebService(string url) {
            if (((url == null) 
                        || (url == string.Empty))) {
                return false;
            }
            System.Uri wsUri = new System.Uri(url);
            if (((wsUri.Port >= 1024) 
                        && (string.Compare(wsUri.Host, "localHost", System.StringComparison.OrdinalIgnoreCase) == 0))) {
                return true;
            }
            return false;
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class RegistrationEntry {
        
        private string typeField;
        
        private ServiceInterface[] serviceInterfacesField;
        
        private Database[] databasesField;
        
        private EventType[] eventTypesField;
        
        private ArtifactType[] artifactTypesField;
        
        private RegistrationExtendedAttribute[] registrationExtendedAttributesField;
        
        private ChangeType changeTypeField;
        
        /// <remarks/>
        public string Type {
            get {
                return this.typeField;
            }
            set {
                this.typeField = value;
            }
        }
        
        /// <remarks/>
        public ServiceInterface[] ServiceInterfaces {
            get {
                return this.serviceInterfacesField;
            }
            set {
                this.serviceInterfacesField = value;
            }
        }
        
        /// <remarks/>
        public Database[] Databases {
            get {
                return this.databasesField;
            }
            set {
                this.databasesField = value;
            }
        }
        
        /// <remarks/>
        public EventType[] EventTypes {
            get {
                return this.eventTypesField;
            }
            set {
                this.eventTypesField = value;
            }
        }
        
        /// <remarks/>
        public ArtifactType[] ArtifactTypes {
            get {
                return this.artifactTypesField;
            }
            set {
                this.artifactTypesField = value;
            }
        }
        
        /// <remarks/>
        public RegistrationExtendedAttribute[] RegistrationExtendedAttributes {
            get {
                return this.registrationExtendedAttributesField;
            }
            set {
                this.registrationExtendedAttributesField = value;
            }
        }
        
        /// <remarks/>
        public ChangeType ChangeType {
            get {
                return this.changeTypeField;
            }
            set {
                this.changeTypeField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class ServiceInterface {
        
        private string nameField;
        
        private string urlField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public string Url {
            get {
                return this.urlField;
            }
            set {
                this.urlField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class RegistrationExtendedAttribute {
        
        private string nameField;
        
        private string valueField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public string Value {
            get {
                return this.valueField;
            }
            set {
                this.valueField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class OutboundLinkType {
        
        private string nameField;
        
        private string targetArtifactTypeToolField;
        
        private string targetArtifactTypeNameField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public string TargetArtifactTypeTool {
            get {
                return this.targetArtifactTypeToolField;
            }
            set {
                this.targetArtifactTypeToolField = value;
            }
        }
        
        /// <remarks/>
        public string TargetArtifactTypeName {
            get {
                return this.targetArtifactTypeNameField;
            }
            set {
                this.targetArtifactTypeNameField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class ArtifactType {
        
        private string nameField;
        
        private OutboundLinkType[] outboundLinkTypesField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public OutboundLinkType[] OutboundLinkTypes {
            get {
                return this.outboundLinkTypesField;
            }
            set {
                this.outboundLinkTypesField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class EventType {
        
        private string nameField;
        
        private string schemaField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public string Schema {
            get {
                return this.schemaField;
            }
            set {
                this.schemaField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public partial class Database {
        
        private string nameField;
        
        private string databaseNameField;
        
        private string sQLServerNameField;
        
        private string connectionStringField;
        
        private bool excludeFromBackupField;
        
        /// <remarks/>
        public string Name {
            get {
                return this.nameField;
            }
            set {
                this.nameField = value;
            }
        }
        
        /// <remarks/>
        public string DatabaseName {
            get {
                return this.databaseNameField;
            }
            set {
                this.databaseNameField = value;
            }
        }
        
        /// <remarks/>
        public string SQLServerName {
            get {
                return this.sQLServerNameField;
            }
            set {
                this.sQLServerNameField = value;
            }
        }
        
        /// <remarks/>
        public string ConnectionString {
            get {
                return this.connectionStringField;
            }
            set {
                this.connectionStringField = value;
            }
        }
        
        /// <remarks/>
        public bool ExcludeFromBackup {
            get {
                return this.excludeFromBackupField;
            }
            set {
                this.excludeFromBackupField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "2.0.50727.1434")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://schemas.microsoft.com/TeamFoundation/2005/06/Services/Registration/03")]
    public enum ChangeType {
        
        /// <remarks/>
        Add,
        
        /// <remarks/>
        Change,
        
        /// <remarks/>
        Delete,
        
        /// <remarks/>
        NoChange,
    }
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "2.0.50727.1434")]
    public delegate void GetRegistrationEntriesCompletedEventHandler(object sender, GetRegistrationEntriesCompletedEventArgs e);
    
    /// <remarks/>
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Web.Services", "2.0.50727.1434")]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    public partial class GetRegistrationEntriesCompletedEventArgs : System.ComponentModel.AsyncCompletedEventArgs {
        
        private object[] results;
        
        internal GetRegistrationEntriesCompletedEventArgs(object[] results, System.Exception exception, bool cancelled, object userState) : 
                base(exception, cancelled, userState) {
            this.results = results;
        }
        
        /// <remarks/>
        public RegistrationEntry[] Result {
            get {
                this.RaiseExceptionIfNecessary();
                return ((RegistrationEntry[])(this.results[0]));
            }
        }
    }
}

#pragma warning restore 1591