﻿using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaJet.Metadata.Core
{
    internal static class Configurator
    {
        internal static void ConfigureSystemProperties(in OneDbMetadataProvider cache, in MetadataObject metadata)
        {
            if (metadata is Catalog catalog)
            {
                ConfigureCatalog(in cache, in catalog);
            }
            else if (metadata is Document document)
            {
                ConfigureDocument(in document);
            }
            else if (metadata is Enumeration enumeration)
            {
                ConfigureEnumeration(in enumeration);
            }
            else if (metadata is Publication publication)
            {
                ConfigurePublication(in cache, in publication);
            }
            else if (metadata is Characteristic characteristic)
            {
                ConfigureCharacteristic(in cache, in characteristic);
            }
            else if (metadata is InformationRegister register1)
            {
                ConfigureInformationRegister(in cache, in register1);
            }
            else if (metadata is AccumulationRegister register2)
            {
                ConfigureAccumulationRegister(in cache, in register2);
            }
            else if (metadata is ChangeTrackingTable changeTable)
            {
                ConfigureChangeTrackingTable(in cache, in changeTable);
            }
        }
        internal static void ConfigureSharedProperties(in OneDbMetadataProvider cache, in MetadataObject metadata)
        {
            if (metadata is Enumeration)
            {
                return;
            }

            if (metadata is not ApplicationObject target)
            {
                return;
            }

            foreach (SharedProperty property in cache.GetMetadataObjects(MetadataTypes.SharedProperty))
            {
                if (property.UsageSettings.TryGetValue(target.Uuid, out SharedPropertyUsage usage))
                {
                    if (usage == SharedPropertyUsage.Use)
                    {
                        target.Properties.Add(property);

                        ConfigureSharedPropertiesForTableParts(target, property);
                    }
                }
                else // Auto
                {
                    if (property.AutomaticUsage == AutomaticUsage.Use)
                    {
                        target.Properties.Add(property);

                        ConfigureSharedPropertiesForTableParts(target, property);
                    }
                }
            }
        }
        internal static void ConfigureSharedPropertiesForTableParts(ApplicationObject owner, SharedProperty property)
        {
            if (owner is Publication)
            {
                return;
            }

            if (property.DataSeparationUsage != DataSeparationUsage.Use)
            {
                return;
            }

            if (property.DataSeparationMode != DataSeparationMode.Independent)
            {
                return;
            }

            if (owner is not ITablePartOwner aggregate)
            {
                return;
            }

            foreach (TablePart table in aggregate.TableParts)
            {
                table.Properties.Add(property);
            }
        }
        internal static void ConfigureDataTypeDescriptor(in OneDbMetadataProvider cache, in DataTypeDescriptor target, in List<Guid> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
            {
                return;
            }

            int count = 0;
            Guid identifier = Guid.Empty;

            for (int i = 0; i < identifiers.Count; i++)
            {
                identifier = identifiers[i];

                if (identifier == Guid.Empty ||
                    identifier == SingleTypes.ValueStorage ||
                    identifier == SingleTypes.UniqueIdentifier)
                {
                    continue;
                }

                count += ResolveAndCountReferenceTypes(in cache, in target, identifier);

                if (count > 1) { break; }
            }

            if (count == 0) // zero reference types
            {
                target.CanBeReference = false;
                target.TypeCode = 0;
                target.Reference = Guid.Empty;
                return;
            }

            if (count == 1) // single reference type
            {
                target.CanBeReference = true;

                if (cache.TryGetReferenceInfo(identifier, out MetadataItem entry))
                {
                    /// Редкий, исключительный случай, но всё-таки надо учесть ¯\_(ツ)_/¯
                    /// Если reference это общий ссылочный тип данных, например, ПланОбменаСсылка <see cref="ReferenceTypes"/>,
                    /// и для этого типа данных пользователем в конфигруации создан только один конкретный тип, например,
                    /// ПланОбмена.МойПланОбмена, то свойства Reference и TypeCode переменной target заполняются пустыми значениями,
                    /// что соответствует множественному ссылочному типу данных, и это, как следствие, приводит к тому, что
                    /// свойство IsMultipleType класса <see cref="DataTypeDescriptor"/> возвращает некорректное значение true,
                    /// что в свою очередь приводит к некорректному формированию метаданных полей базы данных для такого свойства
                    /// объекта метаданных в процедуре <see cref="ConfigureDatabaseColumns"/>.
                    /// Таким образом в конфигураторе типом свойства объекта метаданных (или табличной части) является общий тип данных,
                    /// но на самом деле на уровне базы данных он интерпретируется и используется как конкретный тип данных.
                    /// Другими словами там, где обычно генерируется три поля: _Fld123_TYPE, _Fld123_RTRef и _Fld123_RRRef,
                    /// создаётся только одно _Fld123RRef ...

                    Guid uuid = (entry.Uuid == Guid.Empty) // Общий ссылочный тип
                        ? cache.GetSingleMetadataObjectUuid(identifier) // Пытаемся получить единственный конкретный тип
                        : entry.Uuid; // Иначе всё, как обычно

                    target.Reference = uuid; // uuid объекта метаданных

                    if (cache.TryGetDbName(uuid, out DbName db))
                    {
                        target.TypeCode = db.Code; // код типа объекта метаданных
                    }
                }
                else
                {
                    // unsupported reference type, например "БизнесПроцесс"
                    target.TypeCode = 0;
                    target.Reference = identifier;
                }
            }
            else // multiple reference type
            {
                target.CanBeReference = true;
                target.TypeCode = 0;
                target.Reference = Guid.Empty;
            }
        }
        private static int ResolveAndCountReferenceTypes(in OneDbMetadataProvider cache, in DataTypeDescriptor target, Guid reference)
        {
            // RULES (правила разрешения ссылочных типов данных для объекта "ОписаниеТипов"):
            // 1. DataTypeDescriptor (property type) can have only one reference to NamedDataTypeDescriptor or Characteristic
            //    Additional references to another data types are not allowed in this case. (!)
            // 2. NamedDataTypeDescriptor and Characteristic can not reference them self or each other. (!)
            // 3. Если ссылочный тип имеет значение, например, "СправочникСсылка", то есть любой справочник,
            //    в таком случае необходимо вычислить количество справочников в составе конфигурации:
            //    если возможным справочником будет только один, то это будет single reference type. (!)
            // 4. То же самое, что и для пункта #3, касается значения типа "ЛюбаяСсылка". (!)

            if (cache.TryGetCharacteristicDataType(reference, out DataTypeDescriptor descriptor))
            {
                if (descriptor is null) { return 0; } // this should not happen

                target.Apply(in descriptor);

                if (!target.CanBeReference) { return 0; } // no reference types

                if (target.Reference == Guid.Empty)
                {
                    return 2; // multiple reference type (may be more then 2 in fact)
                }
                else
                {
                    return 1; // single reference type
                }
            }

            #region "Deprecated code - remove in the future"
            //if (cache.TryResolveCharacteristic(reference, out Guid uuid))
            //{
            //    // NOTE: Lazy-load of Characteristic: recursion is avoided because of rule #2.
            //    // NOTE: 08.03.2024
            //    // Рекурсия возможна, например, при загрузке в кэш объекта метаданных "МойПланВидовХарактеристик",
            //    // который имеет реквизит с типом данных "Характеристика.МойПланВидовХарактеристик".
            //    // При попытке разрешить ссылку на характеристику попадаем сюда и уходим в рекурсию:
            //    // reference =         Характеристика.МойПланВидовХарактеристик
            //    // uuid      = ПланВидовХарактеристик.МойПланВидовХарактеристик
            //    // Решение: загрузка типов значенией характеристик при первичном заполнении кэша метаданных.
            //    /// <see cref="DaJet.Metadata.Parsers.CharacteristicParser.Parse(in ConfigFileReader, Guid, out MetadataInfo)" />
            //    /// <see cref="DaJet.Metadata.Parsers.CharacteristicParser.DataTypeDescriptor" />
                 
            //    MetadataObject metadata = cache.GetMetadataObjectCached(MetadataTypes.Characteristic, uuid);

            //    if (metadata is not Characteristic characteristic)
            //    {
            //        return 0; // this should not happen
            //    }

            //    target.Apply(characteristic.DataTypeDescriptor);
                
            //    if (!target.CanBeReference)
            //    {
            //        return 0; // no reference types
            //    }

            //    if (target.Reference == Guid.Empty)
            //    {
            //        return 2; // multiple reference type (may be more then 2 in fact)
            //    }
            //    else
            //    {
            //        return 1; // single reference type
            //    }
            //}
            #endregion

            if (cache.TryGetReferenceInfo(reference, out MetadataItem info))
            {
                if (info.Type == MetadataTypes.NamedDataTypeDescriptor)
                {
                    // NOTE: Lazy-load of NamedDataTypeDescriptor: recursion is avoided because of rule #2.
                    MetadataObject metadata = cache.GetMetadataObjectCached(info.Type, info.Uuid);

                    if (metadata is not NamedDataTypeDescriptor named)
                    {
                        return 0; // this should not happen
                    }

                    target.Apply(named.DataTypeDescriptor);

                    if (!target.CanBeReference)
                    {
                        return 0; // no reference types
                    }

                    if (target.Reference == Guid.Empty)
                    {
                        return 2; // multiple reference type (may be more then 2 in fact)
                    }
                    else
                    {
                        return 1; // single reference type
                    }
                }
            }

            if (info.Uuid != Guid.Empty)
            {
                return 1; // single reference type, and definitely not general type (see code below)
            }

            int count = 0;

            if (reference == ReferenceTypes.Catalog)
            {
                count = cache.CountMetadataObjects(MetadataTypes.Catalog);
            }
            else if (reference == ReferenceTypes.Document)
            {
                count = cache.CountMetadataObjects(MetadataTypes.Document);
            }
            else if (reference == ReferenceTypes.Enumeration)
            {
                count = cache.CountMetadataObjects(MetadataTypes.Enumeration);
            }
            else if (reference == ReferenceTypes.Publication)
            {
                count = cache.CountMetadataObjects(MetadataTypes.Publication);
            }
            else if (reference == ReferenceTypes.Characteristic)
            {
                count = cache.CountMetadataObjects(MetadataTypes.Characteristic);
            }
            else if (reference == ReferenceTypes.AnyReference)
            {
                count += cache.CountMetadataObjects(MetadataTypes.Catalog);
                if (count > 1) { return count; }
                count += cache.CountMetadataObjects(MetadataTypes.Document);
                if (count > 1) { return count; }
                count += cache.CountMetadataObjects(MetadataTypes.Enumeration);
                if (count > 1) { return count; }
                count += cache.CountMetadataObjects(MetadataTypes.Publication);
                if (count > 1) { return count; }
                count += cache.CountMetadataObjects(MetadataTypes.Characteristic);
                if (count > 1) { return count; }
            }
            else
            {
                // Неподдерживаемый общий ссылочный тип
                return 1; // single reference type 
            }

            return count;
        }

        #region "TABLE PARTS"

        internal static void ConfigureTableParts(in OneDbMetadataProvider cache, in ApplicationObject owner)
        {
            if (owner is not ITablePartOwner aggregate)
            {
                return;
            }

            foreach (TablePart tablePart in aggregate.TableParts)
            {
                if (cache.Extension != null && string.IsNullOrEmpty(tablePart.TableName))
                {
                    //NOTE (!) Заимствованные из основной конфигурации табличные части в расширениях
                    //не имеют системных свойств (они их наследуют), если только они их не переопределяют.
                    continue;
                }

                ConfigurePropertyСсылка(in owner, in tablePart);
                ConfigurePropertyКлючСтроки(in tablePart);
                ConfigurePropertyНомерСтроки(in cache, in tablePart);
            }
        }
        private static void ConfigurePropertyСсылка(in ApplicationObject owner, in TablePart tablePart)
        {
            MetadataProperty property = new()
            {
                Name = "Ссылка",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = owner.TableName + "_IDRRef"
            };
            
            property.PropertyType.CanBeReference = true;
            property.PropertyType.TypeCode = owner.TypeCode;
            property.PropertyType.Reference = owner.Uuid;

            // Собственная табличная часть расширения, но добавленная к заимствованному объекту основной конфигурации:
            // в таком случае у заимствованного объекта значения TypeCode и Uuid надо искать в основной конфигурации.
            // Однако, в данный момент мы находимся в контексте расширения и контекст основной конфигруации недоступен!
            //if (owner.Parent != Guid.Empty)
            //{
                // TODO: нужно реализовать алгоритм разрешения ссылок на заимствованные объекты
                // в процедуре применения расширения к основной конфиграции!

                //MetadataObject parent = cache.GetMetadataObject(owner.Parent);
            //}

            //TODO: property.PropertyType.References.Add(new MetadataItem(MetadataTypes.Catalog, owner.Uuid, owner.Name));

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary",
                KeyOrdinal = 1,
                IsPrimaryKey = true
            });

            tablePart.Properties.Add(property);
        }
        private static void ConfigurePropertyКлючСтроки(in TablePart tablePart)
        {
            MetadataProperty property = new()
            {
                Name = "KeyField",    // Исправлено на латиницу из-за того, что в некоторых конфигурациях 1С
                Alias = "КлючСтроки", // для реквизитов табличной части иногда используют имя "КлючСтроки".
                Uuid = Guid.Empty,    // Это не запрещено 1С в отличие от имён реквизитов "Ссылка" и "НомерСтроки".
                Purpose = PropertyPurpose.System,
                DbName = "_KeyField"
            };
            property.PropertyType.IsBinary = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 4,
                TypeName = "binary",
                KeyOrdinal = 2,
                IsPrimaryKey = true
            });

            tablePart.Properties.Add(property);
        }
        private static void ConfigurePropertyНомерСтроки(in OneDbMetadataProvider cache, in TablePart tablePart)
        {
            if (!cache.TryGetLineNo(tablePart.Uuid, out DbName dbn))
            {
                return;
            }

            MetadataProperty property = new()
            {
                Name = "НомерСтроки",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = CreateDbName(dbn.Name, dbn.Code)
            };
            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 5;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Precision = 5,
                TypeName = "numeric"
            });

            tablePart.Properties.Add(property);
        }

        #endregion

        #region "ENUMERATION"

        private static void ConfigureEnumeration(in Enumeration enumeration)
        {
            ConfigurePropertyСсылка(enumeration);
            ConfigurePropertyПорядок(in enumeration);
        }
        private static void ConfigurePropertyПорядок(in Enumeration enumeration)
        {
            MetadataProperty property = new()
            {
                Name = "Порядок",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_EnumOrder"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Scale = 0,
                Precision = 10,
                TypeName = "numeric"
            });

            enumeration.Properties.Add(property);
        }

        #endregion

        #region "CATALOG"

        // Последовательность сериализации системных свойств в формат 1С JDTO
        // 1. ЭтоГруппа        = IsFolder           - bool (invert)
        // 2. Ссылка           = Ref                - uuid 
        // 3. ПометкаУдаления  = DeletionMark       - bool
        // 4. Владелец         = Owner              - { #type + #value }
        // 5. Родитель         = Parent             - uuid
        // 6. Код              = Code               - string | number
        // 7. Наименование     = Description        - string
        // 8. Предопределённый = PredefinedDataName - string

        private static void ConfigureCatalog(in OneDbMetadataProvider cache, in Catalog catalog)
        {
            if (catalog.IsHierarchical)
            {
                if (catalog.HierarchyType == HierarchyType.Groups)
                {
                    ConfigurePropertyЭтоГруппа(catalog);
                }
            }

            ConfigurePropertyСсылка(catalog);
            ConfigurePropertyПометкаУдаления(catalog);

            List<Guid> owners = cache.GetCatalogOwners(catalog.Uuid);

            if (owners != null && owners.Count > 0)
            {
                ConfigurePropertyВладелец(in cache, in catalog, in owners);
            }

            if (catalog.IsHierarchical)
            {
                ConfigurePropertyРодитель(in cache, catalog);
            }

            if (catalog.CodeLength > 0)
            {
                ConfigurePropertyКод(catalog);
            }

            if (catalog.DescriptionLength > 0)
            {
                ConfigurePropertyНаименование(catalog);
            }

            ConfigurePropertyПредопределённый(in cache, catalog);

            ConfigurePropertyВерсияДанных(catalog);
        }
        private static void ConfigurePropertyСсылка(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "Ссылка",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_IDRRef"
            };
            
            property.PropertyType.CanBeReference = true;
            property.PropertyType.TypeCode = metadata.TypeCode;
            property.PropertyType.Reference = metadata.Uuid;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary",
                KeyOrdinal = 1,
                IsPrimaryKey = true
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyВерсияДанных(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "ВерсияДанных",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Version",
                IsDbGenerated = true
            };
            property.PropertyType.IsBinary = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 8,
                TypeName = "timestamp",
                Purpose = ColumnPurpose.Binary
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyПометкаУдаления(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "ПометкаУдаления",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Marked"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyПредопределённый(in OneDbMetadataProvider cache, in ApplicationObject metadata)
        {
            if (cache.InfoBase.CompatibilityVersion >= 80303)
            {
                ConfigurePropertyPredefinedID(metadata);
            }
            else if (metadata is not Publication)
            {
                ConfigurePropertyIsMetadata(metadata);
            }
            else if (cache.InfoBase.CompatibilityVersion >= 80216)
            {
                ConfigurePropertyPredefinedID(metadata);
            }
        }
        private static void ConfigurePropertyIsMetadata(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "Предопределённый",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_IsMetadata"
            };

            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyPredefinedID(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "Предопределённый",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_PredefinedID"
            };

            property.PropertyType.IsUuid = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary"
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyКод(in ApplicationObject metadata)
        {
            if (metadata is not IEntityCode code)
            {
                throw new InvalidOperationException($"Metadata object \"{metadata.Name}\" does not implement IReferenceCode interface.");
            }

            MetadataProperty property = new MetadataProperty()
            {
                Name = "Код",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Code"
            };

            if (code.CodeType == CodeType.String)
            {
                property.PropertyType.CanBeString = true;
                property.PropertyType.StringKind = StringKind.Variable;
                property.PropertyType.StringLength = code.CodeLength;

                property.Columns.Add(new MetadataColumn()
                {
                    Name = property.DbName,
                    Length = code.CodeLength,
                    TypeName = "nvarchar"
                });
            }
            else
            {
                property.PropertyType.CanBeNumeric = true;
                property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
                property.PropertyType.NumericPrecision = code.CodeLength;

                property.Columns.Add(new MetadataColumn()
                {
                    Name = property.DbName,
                    Precision = code.CodeLength,
                    TypeName = "numeric"
                });
            }

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyНаименование(in ApplicationObject metadata)
        {
            if (metadata is not IEntityDescription description)
            {
                throw new InvalidOperationException($"Metadata object \"{metadata.Name}\" does not implement IDescription interface.");
            }

            MetadataProperty property = new MetadataProperty()
            {
                Name = "Наименование",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Description"
            };
            property.PropertyType.CanBeString = true;
            property.PropertyType.StringLength = description.DescriptionLength;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = description.DescriptionLength,
                TypeName = "nvarchar"
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyРодитель(in OneDbMetadataProvider cache, in ApplicationObject metadata)
        {
            // This hierarchy property always has the single reference type (adjacency list)

            MetadataProperty property = new()
            {
                Name = "Родитель",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_ParentIDRRef"
            };

            property.PropertyType.CanBeReference = true;
            property.PropertyType.TypeCode = metadata.TypeCode;
            property.PropertyType.Reference = metadata.Uuid;

            Guid type = Guid.Empty;

            if (metadata is Catalog)
            {
                type = MetadataTypes.Catalog;
            }
            else if (metadata is Characteristic)
            {
                type = MetadataTypes.Characteristic;
            }

            //REFACTORING(29.01.2023)
            //if (type != Guid.Empty)
            //{
            //    property.PropertyType.References.Add(new MetadataItem(type, metadata.Uuid, metadata.Name));
            //}

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary"
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyЭтоГруппа(in ApplicationObject metadata)
        {
            MetadataProperty property = new()
            {
                Name = "ЭтоГруппа",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Folder"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary" // инвертировать !
            });

            metadata.Properties.Add(property);
        }
        private static void ConfigurePropertyВладелец(in OneDbMetadataProvider cache, in Catalog catalog, in List<Guid> owners)
        {
            MetadataProperty property = new()
            {
                Name = "Владелец",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_OwnerID"
            };
            property.PropertyType.CanBeReference = true;

            //REFACTORING(29.01.2023)
            //foreach (Guid owner in owners)
            //{
            //    MetadataItem item = cache.GetCatalogOwner(owner);

            //    if (item != MetadataItem.Empty)
            //    {
            //        property.PropertyType.References.Add(item);
            //    }
            //}

            if (owners.Count == 1) // Single type value
            {
                property.PropertyType.Reference = owners[0];

                if (cache.TryGetDbName(owners[0], out DbName dbn))
                {
                    property.PropertyType.TypeCode = dbn.Code;
                }

                property.Columns.Add(new MetadataColumn()
                {
                    Name = "_OwnerIDRRef",
                    Length = 16,
                    TypeName = "binary"
                });
            }
            else // Multiple type value
            {
                property.PropertyType.Reference = Guid.Empty;

                property.Columns.Add(new MetadataColumn()
                {
                    Name = "_OwnerID_TYPE",
                    Length = 1,
                    TypeName = "binary",
                    Purpose = ColumnPurpose.Tag
                });
                property.Columns.Add(new MetadataColumn()
                {
                    Name = "_OwnerID_RTRef",
                    Length = 4,
                    TypeName = "binary",
                    Purpose = ColumnPurpose.TypeCode
                });
                property.Columns.Add(new MetadataColumn()
                {
                    Name = "_OwnerID_RRRef",
                    Length = 16,
                    TypeName = "binary",
                    Purpose = ColumnPurpose.Identity
                });
            }

            catalog.Properties.Add(property);
        }

        #endregion

        #region "CHARACTERISTIC"

        private static void ConfigureCharacteristic(in OneDbMetadataProvider cache, in Characteristic characteristic)
        {
            if (characteristic.IsHierarchical)
            {
                if (characteristic.HierarchyType == HierarchyType.Groups)
                {
                    ConfigurePropertyЭтоГруппа(characteristic);
                }
            }

            ConfigurePropertyСсылка(characteristic);
            ConfigurePropertyПометкаУдаления(characteristic);

            if (characteristic.IsHierarchical)
            {
                ConfigurePropertyРодитель(in cache, characteristic);
            }

            if (characteristic.CodeLength > 0)
            {
                ConfigurePropertyКод(characteristic);
            }

            if (characteristic.DescriptionLength > 0)
            {
                ConfigurePropertyНаименование(characteristic);
            }

            ConfigurePropertyПредопределённый(in cache, characteristic);

            ConfigurePropertyТипЗначения(in characteristic);

            ConfigurePropertyВерсияДанных(characteristic);
        }
        private static void ConfigurePropertyТипЗначения(in Characteristic characteristic)
        {
            MetadataProperty property = new MetadataProperty()
            {
                Name = "ТипЗначения",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Type"
            };
            property.PropertyType.IsBinary = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = -1,
                IsNullable = true,
                TypeName = "varbinary"
            });

            characteristic.Properties.Add(property);
        }

        #endregion

        #region "PUBLICATION"

        private static void ConfigurePublication(in OneDbMetadataProvider cache, in Publication publication)
        {
            ConfigurePropertyСсылка(publication);
            ConfigurePropertyВерсияДанных(publication);
            ConfigurePropertyПометкаУдаления(publication);
            ConfigurePropertyКод(publication);
            ConfigurePropertyНаименование(publication);
            ConfigurePropertyНомерОтправленного(in publication);
            ConfigurePropertyНомерПринятого(in publication);
            ConfigurePropertyПредопределённый(in cache, publication);
        }
        private static void ConfigurePropertyНомерОтправленного(in Publication publication)
        {
            MetadataProperty property = new()
            {
                Name = "НомерОтправленного",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_SentNo"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Scale = 0,
                Precision = 10,
                TypeName = "numeric"
            });

            publication.Properties.Add(property);
        }
        private static void ConfigurePropertyНомерПринятого(in Publication publication)
        {
            MetadataProperty property = new()
            {
                Name = "НомерПринятого",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_ReceivedNo"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Scale = 0,
                Precision = 10,
                TypeName = "numeric"
            });

            publication.Properties.Add(property);
        }

        #endregion

        #region "DOCUMENT"

        // Последовательность сериализации системных свойств в формат 1С JDTO
        // 1. Ссылка          = Ref          - uuid
        // 2. ПометкаУдаления = DeletionMark - bool
        // 3. Дата            = Date         - DateTime
        // 4. Номер           = Number       - string | number
        // 5. Проведён        = Posted       - bool

        private static void ConfigureDocument(in Document document)
        {
            ConfigurePropertyСсылка(document);
            ConfigurePropertyВерсияДанных(document);
            ConfigurePropertyПометкаУдаления(document);

            ConfigurePropertyДата(document);

            if (document.NumberLength > 0)
            {
                if (document.Periodicity != Periodicity.None)
                {
                    ConfigurePropertyПериодНомера(document);
                }

                ConfigurePropertyНомер(document);
            }

            ConfigurePropertyПроведён(document);
        }
        private static void ConfigurePropertyДата(in Document document)
        {
            MetadataProperty property = new MetadataProperty()
            {
                Name = "Дата",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Date_Time"
            };
            property.PropertyType.CanBeDateTime = true;
            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });
            document.Properties.Add(property);
        }
        private static void ConfigurePropertyПериодНомера(in Document document)
        {
            MetadataProperty property = new MetadataProperty()
            {
                Name = "ПериодНомера",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_NumberPrefix"
            };

            property.PropertyType.CanBeDateTime = true;
            property.PropertyType.DateTimePart = DateTimePart.Date;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });
            document.Properties.Add(property);
        }
        private static void ConfigurePropertyНомер(in Document document)
        {
            MetadataProperty property = new MetadataProperty()
            {
                Name = "Номер",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Number"
            };

            if (document.NumberType == NumberType.Number)
            {
                property.PropertyType.CanBeNumeric = true;
                property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
                property.PropertyType.NumericPrecision = document.NumberLength;
                property.Columns.Add(new MetadataColumn()
                {
                    Name = property.DbName,
                    Precision = document.NumberLength,
                    TypeName = "numeric"
                });
            }
            else
            {
                property.PropertyType.CanBeString = true;
                property.PropertyType.StringKind = StringKind.Variable;
                property.PropertyType.StringLength = document.NumberLength;
                property.Columns.Add(new MetadataColumn()
                {
                    Name = property.DbName,
                    Length = document.NumberLength,
                    TypeName = "nvarchar"
                });
            }
            document.Properties.Add(property);
        }
        private static void ConfigurePropertyПроведён(in Document document)
        {
            MetadataProperty property = new MetadataProperty()
            {
                Name = "Проведён",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Posted"
            };

            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });
            document.Properties.Add(property);
        }

        #endregion

        #region "INFORMATION REGISTER"

        // Последовательность сериализации системных свойств в формат 1С JDTO
        // 1. "Регистратор" = Recorder   - uuid { #type + #value }
        // 2. "Период"      = Period     - DateTime
        // 3. "ВидДвижения" = RecordType - string { "Receipt", "Expense" }
        // 4. "Активность"  = Active     - bool
        // 5. _SimpleKey    = binary(16) - uuid (УникальныйИдентификатор)
        //    - версия платформы 8.3.2 и ниже
        //    - только непериодические регистры сведений
        //    - регистр имеет больше одного измерения

        private static void ConfigureInformationRegister(in OneDbMetadataProvider cache, in InformationRegister register)
        {
            if (register.UseRecorder)
            {
                ConfigurePropertyРегистратор(in cache, register);
            }

            if (register.Periodicity != RegisterPeriodicity.None)
            {
                ConfigurePropertyПериод(register);
            }

            if (register.UseRecorder)
            {
                ConfigurePropertyАктивность(register);
                ConfigurePropertyНомерЗаписи(register);
            }

            if (cache.InfoBase.CompatibilityVersion < 80303 &&
                register.Periodicity == RegisterPeriodicity.None)
            {
                int dimensions = 0;

                for (int i = 0; i < register.Properties.Count; i++)
                {
                    if (register.Properties[i].Purpose == PropertyPurpose.Dimension)
                    {
                        dimensions++;
                    }

                    if (dimensions > 1) { break; }
                }

                if (dimensions > 1)
                {
                    ConfigurePropertySimpleKey(register);
                }
            }
        }
        private static void ConfigurePropertyПериод(in ApplicationObject register)
        {
            MetadataProperty property = new()
            {
                Name = "Период",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Period"
            };

            property.PropertyType.CanBeDateTime = true;

            if (register is InformationRegister inforeg)
            {
                if (inforeg.Periodicity == RegisterPeriodicity.Second)
                {
                    property.PropertyType.DateTimePart = DateTimePart.DateTime;
                }
            }

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });

            register.Properties.Add(property);
        }
        private static void ConfigurePropertyНомерЗаписи(in ApplicationObject register)
        {
            MetadataProperty property = new()
            {
                Name = "НомерСтроки",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_LineNo"
            };
            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericPrecision = 9;
            property.PropertyType.NumericScale = 0;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Scale = 0,
                Precision = 9,
                TypeName = "numeric"
            });

            register.Properties.Add(property);
        }
        private static void ConfigurePropertyАктивность(in ApplicationObject register)
        {
            MetadataProperty property = new()
            {
                Name = "Активность",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Active"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            register.Properties.Add(property);
        }
        private static void ConfigurePropertyРегистратор(in OneDbMetadataProvider cache, in ApplicationObject register)
        {
            List<Guid> recorders = cache.GetRegisterRecorders(register.Uuid);

            if (recorders == null || recorders.Count == 0)
            {
                return;
            }

            MetadataProperty property = new()
            {
                Uuid = Guid.Empty,
                Name = "Регистратор",
                Purpose = PropertyPurpose.System,
                DbName = "_Recorder"
            };

            //REFACTORING(29.01.2023)
            //foreach (Guid recorder in recorders)
            //{
            //    MetadataItem item = cache.GetRegisterRecorder(recorder);

            //    if (item != MetadataItem.Empty)
            //    {
            //        property.PropertyType.References.Add(item);
            //    }
            //}

            MetadataColumn field = new()
            {
                Name = "_RecorderRRef",
                Length = 16,
                TypeName = "binary",
                IsPrimaryKey = true,
                Purpose = ColumnPurpose.Default
            };

            property.Columns.Add(field);

            property.PropertyType.CanBeReference = true;

            if (recorders.Count == 1) // Single type value
            {
                property.PropertyType.Reference = recorders[0];

                if (cache.TryGetDbName(recorders[0], out DbName dbn))
                {
                    property.PropertyType.TypeCode = dbn.Code;
                }
            }
            else // Multiple type value
            {
                property.PropertyType.Reference = Guid.Empty;

                field.Purpose = ColumnPurpose.Identity;

                property.Columns.Add(new MetadataColumn()
                {
                    Name = "_RecorderTRef",
                    Length = 4,
                    TypeName = "binary",
                    IsPrimaryKey = true,
                    Purpose = ColumnPurpose.TypeCode
                });
            }

            register.Properties.Add(property);
        }
        private static void ConfigurePropertySimpleKey(in ApplicationObject register)
        {
            MetadataProperty property = new()
            {
                Name = "SimpleKey",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_SimpleKey"
            };
            property.PropertyType.IsUuid = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary"
            });

            register.Properties.Add(property);
        }

        internal static void ConfigureInfoRegisterSettingsTable(in OneDbMetadataProvider cache, in RegisterSettingsTable table)
        {
            if (table.Entity is not ApplicationObject entity)
            {
                return;
            }

            if (!cache.TryGetInfoRgOpt(entity.Uuid, out DbName dbn))
            {
                return;
            }

            table.Uuid = table.Entity.Uuid;
            table.Name = table.Entity.Name + ".Настройки";
            table.Alias = "Таблица настроек регистра сведений";
            table.TypeCode = table.Entity.TypeCode;
            table.TableName = $"_{dbn.Name}{dbn.Code}";

            ConfigurePropertySliceUsing(in table); // _SliceUsing binary(1) boolean
        }
        private static void ConfigurePropertySliceUsing(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "SliceUsing",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_SliceUsing"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            table.Properties.Add(property);
        }

        internal static void ConfigureInfoRegisterSliceLastTable(in OneDbMetadataProvider cache, in RegisterTotalsTable table)
        {
            if (table.Entity is not InformationRegister register)
            {
                return;
            }

            if (!cache.TryGetInfoRgSL(register.Uuid, out DbName dbn))
            {
                return;
            }

            table.Uuid = register.Uuid;
            table.Name = register.Name + ".СрезПоследних";
            table.Alias = "Таблица итогов регистра сведений (срез последних)";
            table.TypeCode = register.TypeCode;
            table.TableName = $"_{dbn.Name}{dbn.Code}";

            foreach (MetadataProperty property in register.Properties)
            {
                if (property.Purpose == PropertyPurpose.System)
                {
                    if (property.Name == "Период" || property.Name == "Регистратор")
                    {
                        table.Properties.Add(property);
                    }
                    
                    continue;
                }

                table.Properties.Add(property);
            }
        }
        internal static void ConfigureInfoRegisterSliceFirstTable(in OneDbMetadataProvider cache, in RegisterTotalsTable table)
        {
            if (table.Entity is not InformationRegister register)
            {
                return;
            }

            if (!cache.TryGetInfoRgSF(register.Uuid, out DbName dbn))
            {
                return;
            }

            table.Uuid = register.Uuid;
            table.Name = register.Name + ".СрезПервых";
            table.Alias = "Таблица итогов регистра сведений (срез первых)";
            table.TypeCode = register.TypeCode;
            table.TableName = $"_{dbn.Name}{dbn.Code}";

            foreach (MetadataProperty property in register.Properties)
            {
                if (property.Purpose == PropertyPurpose.System)
                {
                    if (property.Name == "Период" || property.Name == "Регистратор")
                    {
                        table.Properties.Add(property);
                    }

                    continue;
                }

                table.Properties.Add(property);
            }
        }

        #endregion

        #region "ACCUMULATION REGISTER"

        private static void ConfigureAccumulationRegister(in OneDbMetadataProvider cache, in AccumulationRegister register)
        {
            ConfigurePropertyРегистратор(in cache, register);
            ConfigurePropertyПериод(register);
            ConfigurePropertyНомерЗаписи(register);

            if (register.RegisterKind == RegisterKind.Balance)
            {
                ConfigurePropertyВидДвижения(in register);
            }

            ConfigurePropertyАктивность(register);
        }
        ///<summary>Вид движения <see cref="RecordType"/> регистра накопления остатков</summary>
        private static void ConfigurePropertyВидДвижения(in AccumulationRegister register)
        {
            // Приход = Receipt = 0 
            // Расход = Expense = 1 

            MetadataProperty property = new()
            {
                Name = "ВидДвижения",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_RecordKind"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 1;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Precision = 1,
                TypeName = "numeric"
            });

            register.Properties.Add(property);
        }
        ///<summary>
        ///<b>Справка 1С:Предприятие 8 :</b> Хеш-функция измерений.
        ///<br>Поле присутствует, если количество измерений не позволяет организовать уникальный индекс по измерениям.</br>
        ///</summary>
        private static void ConfigurePropertyDimHash(in AccumulationRegister register)
        {
            MetadataProperty property = new()
            {
                Name = "DimHash",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_DimHash"
            };

            property.PropertyType.CanBeNumeric = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Precision = 10,
                TypeName = "numeric"
            });

            register.Properties.Add(property);
        }
        ///<summary>
        ///Разделитель итогов. Включается специальной настройкой "Разрешить разделение итогов" в конфигураторе.
        ///<br>Используется для параллельной записи документов в таблицу итогов регистра по одинаковым значениям измерений.</br>
        ///</summary>
        private static void ConfigurePropertySplitter(in RegisterTotalsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "Splitter",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Splitter"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 10;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Precision = 10,
                TypeName = "numeric"
            });

            table.Properties.Add(property);
        }
        internal static void ConfigureRegisterTotalsTable(in OneDbMetadataProvider cache, in RegisterTotalsTable table)
        {
            if (table.Entity is not ApplicationObject entity)
            {
                return;
            }

            if (!cache.TryGetAccumRgT(table.Entity.Uuid, out DbName dbn))
            {
                return;
            }

            table.Uuid = table.Entity.Uuid;
            table.Name = table.Entity.Name + ".Итоги";
            table.Alias = "Таблица итогов регистра накопления";
            table.TypeCode = table.Entity.TypeCode;
            table.TableName = $"_{dbn.Name}{dbn.Code}";

            ConfigurePropertyПериод(table);

            foreach (MetadataProperty property in entity.Properties)
            {
                if (property.Purpose == PropertyPurpose.Dimension)
                {
                    table.Properties.Add(property);
                }
            }

            foreach (MetadataProperty property in entity.Properties)
            {
                if (property.Purpose == PropertyPurpose.Measure)
                {
                    table.Properties.Add(property);
                }
            }

            if (entity is not AccumulationRegister register)
            {
                return;
            }

            if (register.UseSplitter) // Разрешить разделение итогов
            {
                ConfigurePropertySplitter(in table);
            }
        }
        internal static void ConfigureRegisterSettingsTable(in OneDbMetadataProvider cache, in RegisterSettingsTable table)
        {
            if (table.Entity is not ApplicationObject entity)
            {
                return;
            }

            if (!cache.TryGetAccumRgOpt(entity.Uuid, out DbName dbn))
            {
                return;
            }

            table.Uuid = table.Entity.Uuid;
            table.Name = table.Entity.Name + ".Настройки";
            table.Alias = "Таблица настроек регистра накопления";
            table.TypeCode = table.Entity.TypeCode;
            table.TableName = $"_{dbn.Name}{dbn.Code}";

            ConfigurePropertyRegID(in table);               // _RegID               binary(16)        Идентификатор объекта метаданных
            ConfigurePropertyPeriod(in table);              // _Period              datetime          Периодичность хранения итогов (не используется)
            ConfigurePropertyActualPeriod(in table);        // _ActualPeriod        binary(1) boolean Использовать текущие итоги
            ConfigurePropertyPeriodicity(in table);         // _Periodicity         numeric(2,0)      Периодичность регистра
            ConfigurePropertyRepetitionFactor(in table);    // _RepetitionFactor    numeric(2,0)      Кратность (не используется)
            ConfigurePropertyUseTotals(in table);           // _UseTotals           numeric(1,0)      Использовать итоги
            ConfigurePropertyMinPeriod(in table);           // _MinPeriod           datetime          Период расчитанных итогов
            ConfigurePropertyUseSplitter(in table);         // _UseSplitter         binary(1) boolean Разрешить разделение итогов
            ConfigurePropertyMinCalculatedPeriod(in table); // _MinCalculatedPeriod datetime          Минимальный период (не используется - ?)
        }
        private static void ConfigurePropertyRegID(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "RegID",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_RegID"
            };

            property.PropertyType.IsUuid = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 16,
                TypeName = "binary"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyPeriod(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "Period",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Period"
            };

            property.PropertyType.CanBeDateTime = true;
            property.PropertyType.DateTimePart = DateTimePart.DateTime;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyActualPeriod(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "ActualPeriod",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_ActualPeriod"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyPeriodicity(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "Periodicity",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_Periodicity"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 2;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Precision = 2,
                TypeName = "numeric"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyRepetitionFactor(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "RepetitionFactor",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_RepetitionFactor"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 2;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Precision = 2,
                TypeName = "numeric"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyUseTotals(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "UseTotals",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_UseTotals"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericPrecision = 1;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 5,
                Precision = 1,
                TypeName = "numeric"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyMinPeriod(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "MinPeriod",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_MinPeriod"
            };

            property.PropertyType.CanBeDateTime = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyUseSplitter(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "UseSplitter",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_UseSplitter"
            };
            property.PropertyType.CanBeBoolean = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 1,
                TypeName = "binary"
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyMinCalculatedPeriod(in RegisterSettingsTable table)
        {
            MetadataProperty property = new()
            {
                Name = "MinCalculatedPeriod",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_MinCalculatedPeriod"
            };

            property.PropertyType.CanBeDateTime = true;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 6,
                Precision = 19,
                TypeName = "datetime2"
            });

            table.Properties.Add(property);
        }

        #endregion

        #region "PREDEFINED VALUES (catalogs and characteristics)"

        public static void ConfigurePredefinedValues(in OneDbMetadataProvider cache, in MetadataObject metadata)
        {
            try
            {
                TryConfigurePredefinedValues(in cache, in metadata);
            }
            catch
            {
                //TODO: нужно доисследовать структуру хранения предопределённых значений
                //ERROR (line 1376) : Unable to cast object of type 'System.String' to type 'DaJet.Metadata.Core.ConfigObject'
            }
        }
        private static void TryConfigurePredefinedValues(in OneDbMetadataProvider cache, in MetadataObject metadata)
        {
            if (metadata is not IPredefinedValueOwner owner) return;

            int predefinedValueUuid = 3;
            int predefinedIsFolder = 4;
            int predefinedValueName = 6;
            int predefinedValueCode = 7;
            int predefinedDescription = 8;

            string fileName = metadata.Uuid.ToString() + ".1c"; // файл с описанием предопределённых элементов
            
            if (metadata is Characteristic)
            {
                fileName = metadata.Uuid.ToString() + ".7";
                predefinedValueName = 5;
                predefinedValueCode = 6;
                predefinedDescription = 7;
            }

            IEntityCode codeInfo = (metadata as IEntityCode);

            ConfigObject configObject;

            using (ConfigFileReader reader = new(cache.DatabaseProvider, cache.ConnectionString, ConfigTables.Config, fileName))
            {
                configObject = new ConfigFileParser().Parse(reader);
            }

            if (configObject == null || configObject.Count == 0)
            {
                return; // Metadata object has no predefined values file in Config table
            }

            ConfigObject parentObject = configObject.GetObject(new int[] { 1, 2, 14, 2 });

            //string RootName = parentObject.GetString(new int[] { 6, 1 }); // имя корня предопределённых элементов = "Элементы" (уровень 0)
            //string RootName = parentObject.GetString(new int[] { 5, 1 }); // имя корня предопределённых элементов = "Характеристики" (уровень 0)

            int propertiesCount = parentObject.GetInt32(new int[] { 2 });
            int predefinedFlag = propertiesCount + 3;
            int childrenValues = propertiesCount + 4;

            int hasChildren = parentObject.GetInt32(new int[] { predefinedFlag }); // флаг наличия предопределённых элементов
            if (hasChildren == 0) return;

            ConfigObject predefinedValues = parentObject.GetObject(new int[] { childrenValues }); // коллекция описаний предопределённых элементов

            int valuesCount = predefinedValues.GetInt32(new int[] { 1 }); // количество предопределённых элементов (уровень 1)

            if (valuesCount == 0) return;

            int valueOffset = 2;
            for (int v = 0; v < valuesCount; v++)
            {
                PredefinedValue pv = new PredefinedValue();

                ConfigObject predefinedValue = predefinedValues.GetObject(new int[] { v + valueOffset });

                pv.Uuid = predefinedValue.GetUuid(new int[] { predefinedValueUuid, 2, 1 });
                pv.Name = predefinedValue.GetString(new int[] { predefinedValueName, 1 });
                pv.IsFolder = (predefinedValue.GetInt32(new int[] { predefinedIsFolder, 1 }) == 1);
                pv.Description = predefinedValue.GetString(new int[] { predefinedDescription, 1 });

                if (codeInfo != null && codeInfo.CodeLength > 0)
                {
                    pv.Code = predefinedValue.GetString(new int[] { predefinedValueCode, 1 });
                }

                owner.PredefinedValues.Add(pv);

                int haveChildren = predefinedValue.GetInt32(new int[] { 9 }); // флаг наличия дочерних предопределённых элементов (0 - нет, 1 - есть)
                if (haveChildren == 1)
                {
                    ConfigObject children = predefinedValue.GetObject(new int[] { 10 }); // коллекция описаний дочерних предопределённых элементов

                    ConfigurePredefinedValue(children, pv, metadata);
                }
            }
        }
        private static void ConfigurePredefinedValue(ConfigObject predefinedValues, PredefinedValue parent, MetadataObject owner)
        {
            int valuesCount = predefinedValues.GetInt32(new int[] { 1 }); // количество предопределённых элементов (уровень N)

            if (valuesCount == 0) return;

            int predefinedValueUuid = 3;
            int predefinedIsFolder = 4;
            int predefinedValueName = 6;
            int predefinedValueCode = 7;
            int predefinedDescription = 8;

            if (owner is Characteristic)
            {
                predefinedValueName = 5;
                predefinedValueCode = 6;
                predefinedDescription = 7;
            }

            IEntityCode codeInfo = (owner as IEntityCode);

            int valueOffset = 2;
            for (int v = 0; v < valuesCount; v++)
            {
                PredefinedValue pv = new PredefinedValue();

                ConfigObject predefinedValue = predefinedValues.GetObject(new int[] { v + valueOffset });

                pv.Uuid = predefinedValue.GetUuid(new int[] { predefinedValueUuid, 2, 1 });
                pv.Name = predefinedValue.GetString(new int[] { predefinedValueName, 1 });
                pv.IsFolder = (predefinedValue.GetInt32(new int[] { predefinedIsFolder, 1 }) == 1);
                pv.Description = predefinedValue.GetString(new int[] { predefinedDescription, 1 });

                if (codeInfo != null && codeInfo.CodeLength > 0)
                {
                    pv.Code = predefinedValue.GetString(new int[] { predefinedValueCode, 1 });
                }

                parent.Children.Add(pv);

                int haveChildren = predefinedValue.GetInt32(new int[] { 9 }); // флаг наличия дочерних предопределённых элементов (0 - нет, 1 - есть)
                if (haveChildren == 1)
                {
                    ConfigObject children = predefinedValue.GetObject(new int[] { 10 }); // коллекция описаний дочерних предопределённых элементов

                    ConfigurePredefinedValue(children, pv, owner);
                }
            }
        }

        #endregion

        #region "PUBLICATION ARTICLES"

        public static void ConfigureArticles(in OneDbMetadataProvider cache, in Publication publication)
        {
            string fileName = publication.Uuid.ToString() + ".1"; // файл описания состава плана обмена

            ConfigObject configObject;

            using (ConfigFileReader reader = new(cache.DatabaseProvider, cache.ConnectionString, ConfigTables.Config, fileName))
            {
                configObject = new ConfigFileParser().Parse(reader);
            }

            if (configObject == null || configObject.Count == 0)
            {
                return; // Publication has no articles file in Config table
            }

            int count = configObject.GetInt32(new int[] { 1 }); // количество объектов в составе плана обмена

            if (count == 0)
            {
                return;
            }

            int offset = 2;

            for (int i = 1; i <= count; i++)
            {
                Guid uuid = configObject.GetUuid(new int[] { i * offset });

                AutoPublication setting = (AutoPublication)configObject.GetInt32(new int[] { (i * offset) + 1 });

                publication.Articles.Add(uuid, setting);
            }
        }

        #endregion

        #region "CONFIGURE DATABASE NAMES"

        private static string CreateDbName(string token, int code)
        {
            return $"_{token}{code}";

            //if (_provider == DatabaseProvider.SqlServer)
            //{
            //    return $"_{token}{code}";
            //}
            //
            //return $"_{token}{code}".ToLowerInvariant();
        }
        internal static void ConfigureDatabaseNames(in OneDbMetadataProvider cache, in MetadataObject metadata)
        {
            if (metadata is SharedProperty property)
            {
                if (!cache.TryGetDbName(metadata.Uuid, out DbName entry))
                {
                    // Для общего реквизита не настроено ни одного объекта метаданных для использования
                    // или общий реквизит является заимствованным объектом метаданных расширения
                    return; 
                }
                property.DbName = CreateDbName(entry.Name, entry.Code);
                ConfigureDatabaseColumns(in cache, property);
                return;
            }

            if (metadata is not ApplicationObject entity) { return; }

            // NOTE: Заимствованный из основной конфигурации, объект метаданных
            // расширения не имеет записи сопоставления в файле DBNames-Ext-...

            if (!cache.TryGetDbName(metadata.Uuid, out DbName dbn))
            {
                if (cache.Extension == null) // Это основная конфигурация
                {
                    return; //FIXME: this should not happen - log error : DbNames file is broken
                }
                else // Это контекст расширения
                {
                    // Заимствованный из основной конфигурации, объект метаданных расширения
                }
            }
            else
            {
                entity.TypeCode = dbn.Code;
                entity.TableName = CreateDbName(dbn.Name, dbn.Code);
            }

            //NOTE: Если сопоставление DbName не найдено, то здесь мы конфигурируем расширение:
            //это заимствованный объект основной конфигурации, а значит, что TableName = null и TypeCode = 0

            ConfigureDatabaseProperties(in cache, in entity);
            
            ConfigureDatabaseTableParts(in cache, in entity);
        }
        private static void ConfigureDatabaseProperties(in OneDbMetadataProvider cache, in ApplicationObject entity)
        {
            foreach (MetadataProperty property in entity.Properties)
            {
                if (property is SharedProperty)
                {
                    continue;
                }

                if (property.Purpose == PropertyPurpose.System)
                {
                    continue;
                }

                if (!cache.TryGetDbName(property.Uuid, out DbName entry))
                {
                    continue;
                }

                property.DbName = CreateDbName(entry.Name, entry.Code);

                ConfigureDatabaseColumns(in cache, in property);
            }
        }
        private static void ConfigureDatabaseTableParts(in OneDbMetadataProvider cache, in ApplicationObject entity)
        {
            if (entity is not ITablePartOwner aggregate)
            {
                return;
            }

            foreach (TablePart tablePart in aggregate.TableParts)
            {
                // NOTE: Заимствованный из основной конфигурации, объект метаданных
                // расширения не имеет записи сопоставления в файле DBNames-Ext-...

                if (!cache.TryGetDbName(tablePart.Uuid, out DbName entry))
                {
                    if (cache.Extension == null) // Это основная конфигурация
                    {
                        continue; //FIXME: this should not happen - log error : DbNames file is broken
                    }
                    else // Это контекст расширения
                    {
                        // Заимствованный из основной конфигурации, объект метаданных расширения
                    }
                }
                else
                {
                    tablePart.TypeCode = entry.Code;
                    tablePart.TableName = entity.TableName + CreateDbName(entry.Name, entry.Code);
                }

                //NOTE: Если сопоставление DbName не найдено, то здесь мы конфигурируем в контексте расширения:
                //это заимствованная табличная часть объекта основной конфигурации, а значит, что TableName = null и TypeCode = 0

                ConfigureDatabaseProperties(in cache, tablePart);
            }
        }
        private static void ConfigureDatabaseColumns(in OneDbMetadataProvider cache, in MetadataProperty property)
        {
            if (property.PropertyType.IsMultipleType)
            {
                ConfigureDatabaseColumnsForMultipleType(in property);
            }
            else
            {
                ConfigureDatabaseColumnsForSingleType(in property);
            }
        }
        private static void ConfigureDatabaseColumnsForSingleType(in MetadataProperty property)
        {
            if (property.PropertyType.IsUuid)
            {
                property.Columns.Add(new MetadataColumn(property.DbName, "binary", 16)); // bytea
            }
            else if (property.PropertyType.IsBinary)
            {
                // This should not happen (_B):
                // is used only for system properties of system types
            }
            else if (property.PropertyType.IsValueStorage)
            {
                property.Columns.Add(new MetadataColumn(property.DbName, "varbinary", -1)); // bytea
            }
            else if (property.PropertyType.CanBeString)
            {
                if (property.PropertyType.StringKind == StringKind.Fixed)
                {
                    property.Columns.Add(new MetadataColumn(property.DbName, "nchar", property.PropertyType.StringLength)); // mchar
                }
                else
                {
                    property.Columns.Add(new MetadataColumn(property.DbName, "nvarchar", property.PropertyType.StringLength)); // mvarchar
                }
            }
            else if (property.PropertyType.CanBeNumeric)
            {
                // length can be updated from database
                property.Columns.Add(new MetadataColumn(
                    property.DbName,
                    "numeric", 9,
                    property.PropertyType.NumericPrecision,
                    property.PropertyType.NumericScale));
            }
            else if (property.PropertyType.CanBeBoolean)
            {
                property.Columns.Add(new MetadataColumn(property.DbName, "binary", 1)); // boolean
            }
            else if (property.PropertyType.CanBeDateTime)
            {
                // length, precision and scale can be updated from database
                property.Columns.Add(new MetadataColumn(property.DbName, "datetime2", 6, 19, 0)); // "timestamp without time zone"
            }
            else if (property.PropertyType.CanBeReference)
            {
                property.Columns.Add(new MetadataColumn(property.DbName + MetadataTokens.RRef, "binary", 16)); // bytea
            }
        }
        private static void ConfigureDatabaseColumnsForMultipleType(in MetadataProperty property)
        {
            property.Columns.Add(new MetadataColumn(property.DbName + "_" + MetadataTokens.TYPE, "binary", 1)
            {
                Purpose = ColumnPurpose.Tag
            });

            if (property.PropertyType.CanBeBoolean)
            {
                property.Columns.Add(new MetadataColumn(property.DbName + "_" + MetadataTokens.L, "binary", 1)
                {
                    Purpose = ColumnPurpose.Boolean
                });
            }

            if (property.PropertyType.CanBeNumeric)
            {
                // length can be updated from database
                property.Columns.Add(new MetadataColumn(
                    property.DbName + "_" + MetadataTokens.N,
                    "numeric", 9,
                    property.PropertyType.NumericPrecision,
                    property.PropertyType.NumericScale)
                {
                    Purpose = ColumnPurpose.Numeric
                });
            }

            if (property.PropertyType.CanBeDateTime)
            {
                // length, precision and scale can be updated from database
                property.Columns.Add(new MetadataColumn(property.DbName + "_" + MetadataTokens.T, "datetime2", 6, 19, 0)
                {
                    Purpose = ColumnPurpose.DateTime
                });
            }

            if (property.PropertyType.CanBeString)
            {
                if (property.PropertyType.StringKind == StringKind.Fixed)
                {
                    
                    property.Columns.Add(new MetadataColumn(
                        property.DbName + "_" + MetadataTokens.S,
                        "nchar",
                        property.PropertyType.StringLength)
                    {
                        Purpose = ColumnPurpose.String
                    });
                }
                else
                {
                    
                    property.Columns.Add(new MetadataColumn(
                        property.DbName + "_" + MetadataTokens.S,
                        "nvarchar",
                        property.PropertyType.StringLength)
                    {
                        Purpose = ColumnPurpose.String
                    });
                }
            }
            
            if (property.PropertyType.CanBeReference)
            {
                if (property.PropertyType.Reference == Guid.Empty) // miltiple refrence type
                {
                    property.Columns.Add(new MetadataColumn(property.DbName + "_" + MetadataTokens.RTRef, "binary", 4)
                    {
                        Purpose = ColumnPurpose.TypeCode
                    });
                }
                
                property.Columns.Add(new MetadataColumn(property.DbName + "_" + MetadataTokens.RRRef, "binary", 16)
                {
                    Purpose = ColumnPurpose.Identity
                });
            }
        }

        #endregion

        #region "CHANGE TRACKING TABLE"

        internal static void ConfigureChangeTrackingTable(in OneDbMetadataProvider cache, in ChangeTrackingTable table)
        {
            if (table.Entity is not ApplicationObject entity)
            {
                return;
            }

            if (!cache.TryGetChngR(table.Entity.Uuid, out DbName changeTable))
            {
                return;
            }

            table.Uuid = table.Entity.Uuid;
            table.Name = table.Entity.Name + ".Изменения";
            table.Alias = "Таблица регистрации изменений";
            table.TypeCode = table.Entity.TypeCode;
            table.TableName = $"_{changeTable.Name}{changeTable.Code}";

            ConfigurePropertyУзелПланаОбмена(in table);
            ConfigurePropertyНомерСообщения(in table);

            if (entity is Catalog || entity is Document)
            {
                MetadataProperty reference = entity.Properties.Where(p => p.Name == "Ссылка").FirstOrDefault();

                if (reference is not null)
                {
                    table.Properties.Add(reference);
                }
            }
            else if (entity is InformationRegister register)
            {
                if (register.UseRecorder) // Регистр, подчинённый регистратору
                {
                    MetadataProperty recorder = entity.Properties.Where(p => p.Name == "Регистратор").FirstOrDefault();

                    if (recorder is not null)
                    {
                        table.Properties.Add(recorder);
                    }
                }
                else if (register.Periodicity != RegisterPeriodicity.None) // Периодический регистр сведений
                {
                    if (register.UsePeriodForChangeTracking)
                    {
                        MetadataProperty period = entity.Properties.Where(p => p.Name == "Период").FirstOrDefault();

                        if (period is not null)
                        {
                            table.Properties.Add(period);
                        }
                    }

                    foreach (MetadataProperty property in register.Properties)
                    {
                        if (property.Purpose == PropertyPurpose.Dimension && property.UseForChangeTracking)
                        {
                            table.Properties.Add(property);
                        }
                    }
                }
                else // Непериодический и независимый регистр сведений
                {
                    foreach (MetadataProperty property in register.Properties)
                    {
                        if (property.Purpose == PropertyPurpose.Dimension && property.UseForChangeTracking)
                        {
                            table.Properties.Add(property);
                        }
                    }
                }
            }
        }
        private static void ConfigurePropertyУзелПланаОбмена(in ChangeTrackingTable table)
        {
            // This property always has the multiple refrence type,
            // even if there is only one exchange plan configured.

            MetadataProperty property = new()
            {
                Uuid = Guid.Empty,
                Name = "УзелОбмена",
                Purpose = PropertyPurpose.System,
                DbName = "_Node"
            };

            property.PropertyType.CanBeReference = true;
            property.PropertyType.Reference = Guid.Empty;
            //REFACTORING (29.01.2023) property.PropertyType.References.Add(new MetadataItem(ReferenceTypes.Publication, Guid.Empty, "ПланОбменаСсылка"));

            property.Columns.Add(new MetadataColumn()
            {
                Name = "_NodeTRef",
                Length = 4,
                TypeName = "binary",
                KeyOrdinal = 2,
                IsPrimaryKey = true,
                Purpose = ColumnPurpose.TypeCode
            });

            property.Columns.Add(new MetadataColumn()
            {
                Name = "_NodeRRef",
                Length = 16,
                TypeName = "binary",
                KeyOrdinal = 3,
                IsPrimaryKey = true,
                Purpose = ColumnPurpose.Identity
            });

            table.Properties.Add(property);
        }
        private static void ConfigurePropertyНомерСообщения(in ChangeTrackingTable table)
        {
            MetadataProperty property = new()
            {
                Name = "НомерСообщения",
                Uuid = Guid.Empty,
                Purpose = PropertyPurpose.System,
                DbName = "_MessageNo"
            };

            property.PropertyType.CanBeNumeric = true;
            property.PropertyType.NumericKind = NumericKind.AlwaysPositive;
            property.PropertyType.NumericScale = 0;
            property.PropertyType.NumericPrecision = 10;

            property.Columns.Add(new MetadataColumn()
            {
                Name = property.DbName,
                Length = 9,
                Scale = 0,
                Precision = 10,
                TypeName = "numeric"
            });

            table.Properties.Add(property);
        }

        #endregion
    }
}