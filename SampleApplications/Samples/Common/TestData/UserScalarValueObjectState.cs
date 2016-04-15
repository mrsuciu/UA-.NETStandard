/* Copyright (c) 1996-2016, OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation members in good-standing
     - GPL V2: everybody else
   RCL license terms accompanied with this source code. See http://opcfoundation.org/License/RCL/1.00/
   GNU General Public License as published by the Free Software Foundation;
   version 2 of the License are accompanied with this source code. See http://opcfoundation.org/License/GPLv2
   This source code is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/

using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Reflection;
using Opc.Ua;

namespace TestData
{
    public partial class UserScalarValueObjectState
    {
        #region Initialization
        /// <summary>
        /// Initializes the object as a collection of counters which change value on read.
        /// </summary>
        protected override void OnAfterCreate(ISystemContext context, NodeState node)
        {
            base.OnAfterCreate(context, node);

            InitializeVariable(context, BooleanValue, TestData.Variables.UserScalarValueObjectType_BooleanValue);
            InitializeVariable(context, SByteValue, TestData.Variables.UserScalarValueObjectType_SByteValue);
            InitializeVariable(context, ByteValue, TestData.Variables.UserScalarValueObjectType_ByteValue);
            InitializeVariable(context, Int16Value, TestData.Variables.UserScalarValueObjectType_Int16Value);
            InitializeVariable(context, UInt16Value, TestData.Variables.UserScalarValueObjectType_UInt16Value);
            InitializeVariable(context, Int32Value, TestData.Variables.UserScalarValueObjectType_Int32Value);
            InitializeVariable(context, UInt32Value, TestData.Variables.UserScalarValueObjectType_UInt32Value);
            InitializeVariable(context, Int64Value, TestData.Variables.UserScalarValueObjectType_Int64Value);
            InitializeVariable(context, UInt64Value, TestData.Variables.UserScalarValueObjectType_UInt64Value);
            InitializeVariable(context, FloatValue, TestData.Variables.UserScalarValueObjectType_FloatValue);
            InitializeVariable(context, DoubleValue, TestData.Variables.UserScalarValueObjectType_DoubleValue);
            InitializeVariable(context, StringValue, TestData.Variables.UserScalarValueObjectType_StringValue);
            InitializeVariable(context, DateTimeValue, TestData.Variables.UserScalarValueObjectType_DateTimeValue);
            InitializeVariable(context, GuidValue, TestData.Variables.UserScalarValueObjectType_GuidValue);
            InitializeVariable(context, ByteStringValue, TestData.Variables.UserScalarValueObjectType_ByteStringValue);
            InitializeVariable(context, XmlElementValue, TestData.Variables.UserScalarValueObjectType_XmlElementValue);
            InitializeVariable(context, NodeIdValue, TestData.Variables.UserScalarValueObjectType_NodeIdValue);
            InitializeVariable(context, ExpandedNodeIdValue, TestData.Variables.UserScalarValueObjectType_ExpandedNodeIdValue);
            InitializeVariable(context, QualifiedNameValue, TestData.Variables.UserScalarValueObjectType_QualifiedNameValue);
            InitializeVariable(context, LocalizedTextValue, TestData.Variables.UserScalarValueObjectType_LocalizedTextValue);
            InitializeVariable(context, StatusCodeValue, TestData.Variables.UserScalarValueObjectType_StatusCodeValue);
            InitializeVariable(context, VariantValue, TestData.Variables.UserScalarValueObjectType_VariantValue);
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Handles the generate values method.
        /// </summary>
        protected override ServiceResult OnGenerateValues(
            ISystemContext context,
            MethodState method,
            NodeId objectId,
            uint count)
        {
            TestDataSystem system = context.SystemHandle as TestDataSystem;

            if (system == null)
            {
                return StatusCodes.BadOutOfService;
            }

            GenerateValue(system, BooleanValue);
            GenerateValue(system, SByteValue);
            GenerateValue(system, ByteValue);
            GenerateValue(system, Int16Value);
            GenerateValue(system, UInt16Value);
            GenerateValue(system, Int32Value);
            GenerateValue(system, UInt32Value);
            GenerateValue(system, UInt32Value);
            GenerateValue(system, Int64Value);
            GenerateValue(system, UInt64Value);
            GenerateValue(system, FloatValue);
            GenerateValue(system, DoubleValue);
            GenerateValue(system, StringValue);
            GenerateValue(system, DateTimeValue);
            GenerateValue(system, GuidValue);
            GenerateValue(system, ByteStringValue);
            GenerateValue(system, XmlElementValue);
            GenerateValue(system, NodeIdValue);
            GenerateValue(system, ExpandedNodeIdValue);
            GenerateValue(system, QualifiedNameValue);
            GenerateValue(system, LocalizedTextValue);
            GenerateValue(system, StatusCodeValue);
            GenerateValue(system, VariantValue);

            return base.OnGenerateValues(context, method, objectId, count);
        }    
        #endregion
    }
}