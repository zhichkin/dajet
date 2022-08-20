using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaJet.Metadata.Services
{
    public interface IMetaDiffService
    {
        MetaDiff Compare(InfoBase target, InfoBase source);
    }
    public sealed class MetaDiffService : IMetaDiffService
    {
        public MetaDiff Compare(InfoBase target, InfoBase source)
        {
            MetaDiff root = new MetaDiff(null, target, DiffType.None);
            //CompareLists(root, target.Catalogs.Values.ToList(), source.Catalogs.Values.ToList());
            //CompareLists(root, target.Documents.Values.ToList(), source.Documents.Values.ToList());
            //CompareLists(root, target.Characteristics.Values.ToList(), source.Characteristics.Values.ToList());
            //CompareLists(root, target.InformationRegisters.Values.ToList(), source.InformationRegisters.Values.ToList());
            //CompareLists(root, target.AccumulationRegisters.Values.ToList(), source.AccumulationRegisters.Values.ToList());
            return root;
        }
        private void CompareLists<T>(MetaDiff parent, List<T> target_list, List<T> source_list) where T : MetadataObject, IComparable
        {
            int target_count = target_list.Count;
            int source_count = source_list.Count;
            int target_index = 0;
            int source_index = 0;
            int compareResult;

            if (target_count == 0 && source_count == 0) return;

            target_list.Sort();
            source_list.Sort();

            while (target_index < target_count)
            {
                if (source_index < source_count)
                {
                    compareResult = target_list[target_index].CompareTo(source_list[source_index]);
                    if (compareResult < 0)
                    {
                        MetaDiff difference = new MetaDiff(parent, target_list[target_index], DiffType.Delete);
                        parent.Children.Add(difference);
                        SetUpdateDiffType(parent);
                        AddChildren(difference);
                        target_index++;
                    }
                    else if (compareResult == 0)
                    {
                        MetaDiff difference = new MetaDiff(parent, target_list[target_index], DiffType.None);
                        CompareListItems(difference, target_list[target_index], source_list[source_index]);
                        if (difference.Difference == DiffType.Update) parent.Children.Add(difference);
                        target_index++;
                        source_index++;
                    }
                    else
                    {
                        MetaDiff difference = new MetaDiff(parent, source_list[source_index], DiffType.Insert);
                        parent.Children.Add(difference);
                        SetUpdateDiffType(parent);
                        AddChildren(difference);
                        source_index++;
                    }
                }
                else
                {
                    MetaDiff difference = new MetaDiff(parent, target_list[target_index], DiffType.Delete);
                    parent.Children.Add(difference);
                    SetUpdateDiffType(parent);
                    AddChildren(difference);
                    target_index++;
                }
            }
            while (source_index < source_count)
            {
                MetaDiff difference = new MetaDiff(parent, source_list[source_index], DiffType.Insert);
                parent.Children.Add(difference);
                SetUpdateDiffType(parent);
                AddChildren(difference);
                source_index++;
            }
        }
        // Compare methods is used by Update difference
        private void CompareListItems(MetaDiff difference, MetadataObject target, MetadataObject source)
        {
            if (target is ApplicationObject)
            {
                CompareObjects(difference, (ApplicationObject)target, (ApplicationObject)source);
            }
            else if (typeof(MetadataProperty) == target.GetType())
            {
                CompareProperties(difference, (MetadataProperty)target, (MetadataProperty)source);
            }
        }
        private void CompareObjects(MetaDiff difference, ApplicationObject target, ApplicationObject source)
        {
            //difference.NewValues.Clear();
            //if (target.TypeCode != source.TypeCode)
            //{
            //    difference.NewValues.Add("TypeCode", source.TypeCode);
            //}
            //if (difference.NewValues.Count > 0) SetUpdateDiffType(difference);
            
            //if (target.Uuid == source.Uuid || target.FileName == source.FileName)
            //{

            //}

            CompareLists(difference, target.Properties, source.Properties);
            //CompareLists(difference, target.TableParts, source.TableParts);
        }
        private void CompareProperties(MetaDiff difference, MetadataProperty target, MetadataProperty source)
        {
            difference.NewValues.Clear();
            if (target.PropertyType.CanBeBoolean != source.PropertyType.CanBeBoolean
                || target.PropertyType.CanBeString != source.PropertyType.CanBeString
                || target.PropertyType.CanBeNumeric != source.PropertyType.CanBeNumeric
                || target.PropertyType.CanBeDateTime != source.PropertyType.CanBeDateTime
                || target.PropertyType.CanBeReference != source.PropertyType.CanBeReference
                || target.PropertyType.IsUuid != source.PropertyType.IsUuid
                || target.PropertyType.IsValueStorage != source.PropertyType.IsValueStorage
                || target.PropertyType.IsMultipleType != source.PropertyType.IsMultipleType)
            {
                difference.NewValues.Add("PropertyType", source.PropertyType);
            }
            if (difference.NewValues.Count > 0) SetUpdateDiffType(difference);
        }
        private void SetUpdateDiffType(MetaDiff difference)
        {
            if (difference.Difference != DiffType.None) return;
            difference.Difference = DiffType.Update;
            MetaDiff parent = difference.Parent;
            while (parent != null)
            {
                parent.Difference = DiffType.Update;
                parent = parent.Parent;
            }
        }
        // Add children is used by Insert and Delete differences
        private void AddChildren(MetaDiff difference)
        {
            if (difference.Target is ApplicationObject)
            {
                AddObjectChildren(difference);
            }
        }
        private void AddObjectChildren(MetaDiff difference)
        {
            ApplicationObject target = (ApplicationObject)difference.Target;
            
            foreach (MetadataProperty child in target.Properties)
            {
                MetaDiff diff = new MetaDiff(difference, child, difference.Difference);
                difference.Children.Add(diff);
            }
            //foreach (TablePart child in target.TableParts)
            //{
            //    MetaDiff diff = new MetaDiff(difference, child, difference.Difference);
            //    difference.Children.Add(diff);
            //    AddObjectChildren(diff);
            //}
        }
    }
}