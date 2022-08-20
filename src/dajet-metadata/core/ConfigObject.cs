using DaJet.Metadata.Services;
using System;
using System.Collections.Generic;

namespace DaJet.Metadata.Core
{
    public sealed class ConfigObject
    {
        public List<object> Values { get; } = new List<object>();

        public ConfigObject this[int i]
        {
            get { return (ConfigObject)Values[i]; }
        }
        public int Count
        {
            get
            {
                return Values.Count;
            }
        }
        public Guid GetUuid(int i)
        {
            return new Guid(GetString(i));
        }
        public int GetInt32(int i)
        {
            return int.Parse((string)Values[i]);
        }
        public string GetString(int i)
        {
            return (string)Values[i];
        }

        public int GetInt32(int[] path)
        {
            if (path.Length == 1)
            {
                return int.Parse((string)this.Values[path[0]]);
            }

            int i = 0;
            List<object> values = this.Values;
            do
            {
                values = ((ConfigObject)values[path[i]]).Values;
                i++;
            }
            while (i < path.Length - 1);

            return int.Parse((string)values[path[i]]);
        }
        public Guid GetUuid(int[] path)
        {
            return new Guid(this.GetString(path));
        }
        public string GetString(int[] path)
        {
            if (path.Length == 1)
            {
                return (string)this.Values[path[0]];
            }

            int i = 0;
            List<object> values = this.Values;
            do
            {
                values = ((ConfigObject)values[path[i]]).Values;
                i++;
            }
            while (i < path.Length - 1);

            return (string)values[path[i]];
        }
        public ConfigObject GetObject(int[] path)
        {
            if (path.Length == 1)
            {
                return (ConfigObject)this.Values[path[0]];
            }

            int i = 0;
            List<object> values = this.Values;
            do
            {
                values = ((ConfigObject)values[path[i]]).Values;
                i++;
            }
            while (i < path.Length - 1);

            return (ConfigObject)values[path[i]];
        }
                
        public DiffObject CompareTo(ConfigObject target)
        {
            DiffObject diff = new DiffObject()
            {
                Path = string.Empty,
                SourceValue = this,
                TargetValue = target,
                DiffKind = DiffKind.None
            };

            CompareObjects(this, target, diff);

            return diff;
        }
        private void CompareObjects(ConfigObject source, ConfigObject target, DiffObject diff)
        {
            int source_count = source.Values.Count;
            int target_count = target.Values.Count;
            int count = Math.Min(source_count, target_count);

            ConfigObject mdSource;
            ConfigObject mdTarget;
            for (int i = 0; i < count; i++) // update
            {
                mdSource = source.Values[i] as ConfigObject;
                mdTarget = target.Values[i] as ConfigObject;

                if (mdSource != null && mdTarget != null)
                {
                    DiffObject newDiff = CreateDiff(diff, DiffKind.Update, mdSource, mdTarget, i);
                    CompareObjects(mdSource, mdTarget, newDiff);
                    if (newDiff.DiffObjects.Count > 0)
                    {
                        diff.DiffObjects.Add(newDiff);
                    }
                }
                else if (mdSource != null || mdTarget != null)
                {
                    diff.DiffObjects.Add(
                            CreateDiff(
                                diff, DiffKind.Update, source.Values[i], target.Values[i], i));
                }
                else
                {
                    if ((string)source.Values[i] != (string)target.Values[i])
                    {
                        diff.DiffObjects.Add(
                            CreateDiff(
                                diff, DiffKind.Update, source.Values[i], target.Values[i], i));
                    }
                }
            }

            if (source_count > target_count) // delete
            {
                for (int i = count; i < source_count; i++)
                {
                    diff.DiffObjects.Add(
                        CreateDiff(
                            diff, DiffKind.Delete, source.Values[i], null, i));
                }
            }
            else if (target_count > source_count) // insert
            {
                for (int i = count; i < target_count; i++)
                {
                    diff.DiffObjects.Add(
                        CreateDiff(
                            diff, DiffKind.Insert, null, target.Values[i], i));
                }
            }
        }
        private DiffObject CreateDiff(DiffObject parent, DiffKind kind, object source, object target, int path)
        {
            return new DiffObject()
            {
                Path = parent.Path + (string.IsNullOrEmpty(parent.Path) ? string.Empty : ".") + path.ToString(),
                SourceValue = source,
                TargetValue = target,
                DiffKind = kind
            };
        }
    }
}