using DaJet.Metadata.Model;

namespace DaJet.DbViewGenerator
{
    internal static class Configurator
    {
        internal static string GetMetadataTypeName(in ApplicationObject metadata)
        {
            if (metadata is Catalog)
            {
                return $"Справочник";
            }
            else if (metadata is Document)
            {
                return $"Документ";
            }
            else if (metadata is InformationRegister)
            {
                return $"РегистрСведений";
            }
            else if (metadata is AccumulationRegister)
            {
                return $"РегистрНакопления";
            }
            else if (metadata is Enumeration)
            {
                return $"Перечисление";
            }
            else if (metadata is Constant)
            {
                return $"Константа";
            }
            else if (metadata is Characteristic)
            {
                return $"ПланВидовХарактеристик";
            }
            else if (metadata is Publication)
            {
                return $"ПланОбмена";
            }

            return "Unsupported";
        }

        internal static string CreateViewName(in ApplicationObject metadata)
        {
            return CreateViewName(in metadata, false);
        }
        internal static string CreateViewName(in ApplicationObject metadata, bool codify)
        {
            if (codify)
            {
                return $"{metadata.TableName}_view";
            }

            return $"{GetMetadataTypeName(metadata)}.{metadata.Name}";
        }
        internal static string CreateViewName(in ApplicationObject owner, in TablePart table)
        {
            return CreateViewName(in owner, in table, false);
        }
        internal static string CreateViewName(in ApplicationObject owner, in TablePart table, bool codify)
        {
            if (codify)
            {
                return $"{table.TableName}_view";
            }

            return $"{GetMetadataTypeName(owner)}.{owner.Name}.{table.Name}";
        }

        internal static string CreateColumnAlias(in MetadataProperty property, in MetadataColumn field)
        {
            if (field.Purpose == ColumnPurpose.Tag)           { return property.Name + "_TYPE"; }
            else if (field.Purpose == ColumnPurpose.TypeCode) { return property.Name + "_TRef"; }
            else if (field.Purpose == ColumnPurpose.Identity) { return property.Name + "_RRef"; }
            else if (field.Purpose == ColumnPurpose.Boolean)  { return property.Name + "_L"; }
            else if (field.Purpose == ColumnPurpose.Numeric)  { return property.Name + "_N"; }
            else if (field.Purpose == ColumnPurpose.DateTime) { return property.Name + "_T"; }
            else if (field.Purpose == ColumnPurpose.String)   { return property.Name + "_S"; }

            return property.Name;
        }
    }
}