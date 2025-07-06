using DaJet.Data;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Scripting
{
    [Function(UDF_TYPEOF.Name)]
    public sealed class UDF_TYPEOF : IUserDefinedFunction
    {
        public const string Name = "TYPEOF";
        private DatabaseProvider _target;
        public Type GetReturnType(in FunctionExpression node) { return typeof(int); }
        public FunctionDescriptor Transpile(in ISqlTranspiler transpiler, in FunctionExpression node, in StringBuilder script)
        {
            if (node.Name != UDF_TYPEOF.Name)
            {
                throw new FormatException($"[TYPEOF] invalid mapping {node.Name}");
            }

            if (node.Parameters.Count == 0)
            {
                throw new FormatException("[TYPEOF] parameter missing");
            }

            if (node.Parameters.Count > 1)
            {
                throw new FormatException("[TYPEOF] too many parameters");
            }

            _target = transpiler.Target;

            FunctionDescriptor descriptor;

            SyntaxNode parameter = node.Parameters[0];

            if (parameter is ColumnReference column)
            {
                descriptor = Transpile(in column, in script);
            }
            else if (parameter is ScalarExpression scalar)
            {
                descriptor = Transpile(in scalar, in script);
            }
            else if (parameter is VariableReference variable)
            {
                descriptor = Transpile(in variable, in script);
            }
            else if (parameter is MemberAccessExpression accessor)
            {
                descriptor = Transpile(in accessor, in script);
            }
            else if (parameter is TypeIdentifier type)
            {
                descriptor = Transpile(in type, in script);
            }
            else
            {
                throw new FormatException("[TYPEOF] invalid parameter type");
            }

            if (descriptor is not null)
            {
                descriptor.Node = node;
            }

            return descriptor;
        }
        
        private FunctionDescriptor Transpile(in ColumnReference column, in StringBuilder script)
        {
            if (column.Mapping is null || column.Mapping.Count == 0)
            {
                throw new FormatException("[TYPEOF] invalid column mapping");
            }

            if (column.Mapping.Count == 1)
            {
                return TranspileEntityProperty(in column, in script);
            }
            
            return TranspileUnionProperty(in column, in script);
        }
        private FunctionDescriptor TranspileEntityProperty(in ColumnReference column, in StringBuilder script)
        {
            if (column.Binding is not MetadataProperty property)
            {
                throw new FormatException("[TYPEOF] invalid column binding");
            }

            if (column.Mapping[0].Type != UnionTag.Entity)
            {
                throw new FormatException("[TYPEOF] invalid column type");
            }

            script.Append(property.PropertyType.TypeCode);

            return null;
        }
        private FunctionDescriptor TranspileUnionProperty(in ColumnReference column, in StringBuilder script)
        {
            ColumnMapper _type = null; // _TYPE
            ColumnMapper _tref = null; // _TRef

            if (column.Binding is not MetadataProperty property)
            {
                throw new FormatException("[TYPEOF] invalid column type");
            }

            for (int i = 0; i < column.Mapping.Count; i++)
            {
                if (column.Mapping[i].Type == UnionTag.Tag)
                {
                    _type = column.Mapping[i]; // _TYPE
                }

                if (column.Mapping[i].Type == UnionTag.TypeCode)
                {
                    _tref = column.Mapping[i]; // _TRef
                }
            }

            if (_type is null && _tref is null)
            {
                throw new FormatException("[TYPEOF] invalid column type");
            }

            if (_target == DatabaseProvider.SqlServer)
            {
                if (_type is not null)
                {
                    if (_tref is null)
                    {
                        if (property.PropertyType.CanBeReference)
                        {
                            script.Append($"CAST(CASE WHEN {_type.Name} = 0x08 THEN {property.PropertyType.TypeCode} ELSE 0x000000 + {_type.Name} END AS int)");
                        }
                        else
                        {
                            script.Append($"CAST(0x000000 + {_type.Name} AS int)");
                        }
                    }
                    else
                    {
                        script.Append($"CAST(CASE WHEN {_type.Name} = 0x08 THEN {_tref.Name} ELSE 0x000000 + {_type.Name} END AS int)");
                    }
                }
                else
                {
                    script.Append($"CAST({_tref.Name} AS int)");
                }
            }
            else if (_target == DatabaseProvider.PostgreSql)
            {
                string _type_value = "CAST(E'\\\\x08' AS bytea)";
                string _tref_value = string.Empty;

                if (_tref is not null)
                {
                    _tref_value = $"(get_byte({_tref.Name}, 0) << 24) | (get_byte({_tref.Name}, 1) << 16) | (get_byte({_tref.Name}, 2) << 8) | get_byte({_tref.Name}, 3)";
                }

                if (_type is not null)
                {
                    if (_tref is null)
                    {
                        if (property.PropertyType.CanBeReference)
                        {
                            script.Append($"(CASE WHEN {_type.Name} = {_type_value} THEN {property.PropertyType.TypeCode} ELSE get_byte({_type.Name}, 0) END)");
                        }
                        else
                        {
                            script.Append($"get_byte({_type.Name}, 0)");
                        }
                    }
                    else
                    {
                        script.Append($"(CASE WHEN {_type.Name} = {_type_value} THEN {_tref_value} ELSE get_byte({_type.Name}, 0) END)");
                    }
                }
                else
                {
                    script.Append(_tref_value);
                }
            }
            else
            {
                throw new FormatException($"[TYPEOF] unsupported database type {_target}");
            }

            return null;
        }

        private FunctionDescriptor Transpile(in TypeIdentifier type, in StringBuilder script)
        {
            int typeCode = 0;

            if (type.Identifier == "boolean")
            {
                typeCode = (int)UnionTag.Boolean;
            }
            else if (type.Identifier == "number" || type.Identifier == "decimal")
            {
                typeCode = (int)UnionTag.Numeric;
            }
            else if (type.Identifier == "datetime")
            {
                typeCode = (int)UnionTag.DateTime;
            }
            else if (type.Identifier == "string")
            {
                typeCode = (int)UnionTag.String;
            }
            else if (type.Identifier == "entity")
            {
                typeCode = (int)UnionTag.Entity;
            }
            //else if (type.Identifier == "integer")
            //{
            //    typeCode = (int)UnionTag.Integer; //THINK: ??!
            //}

            script.Append(typeCode);

            return null;
        }
        private FunctionDescriptor Transpile(in ScalarExpression scalar, in StringBuilder script)
        {
            if (scalar.Token != TokenType.Entity)
            {
                throw new FormatException("[TYPEOF] invalid scalar type");
            }

            Entity value = Entity.Parse(scalar.Literal);

            script.Append(value.TypeCode);

            return null;
        }
        private FunctionDescriptor Transpile(in VariableReference variable, in StringBuilder script)
        {
            if (variable.Binding is not Entity && !(variable.Binding is Type type && type == typeof(string)))
            {
                throw new FormatException("[TYPEOF] invalid variable type: Entity or string expected");
            }

            string parameterName = $"@TYPEOF_" + variable.Identifier[1..];

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = GetReturnType(null)
            };

            return descriptor;
        }
        private FunctionDescriptor Transpile(in MemberAccessExpression accessor, in StringBuilder script)
        {
            if (accessor.Binding is not Type type)
            {
                throw new FormatException("[TYPEOF] invalid property binding");
            }

            if (type != typeof(Entity))
            {
                throw new FormatException("[TYPEOF] invalid property type");
            }

            string parameterName = $"@TYPEOF_" + accessor.Identifier[1..].Replace('.', '_');

            script.Append(parameterName);

            FunctionDescriptor descriptor = new()
            {
                Target = parameterName,
                ReturnType = GetReturnType(null)
            };

            return descriptor;
        }
    }
}