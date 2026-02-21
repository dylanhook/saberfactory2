using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberFactory2.Modifiers
{
    [Serializable]
    public class TransformModifier
    {
        public string Name;
        public int Id;
#if !UNITY
        [Newtonsoft.Json.JsonIgnore]
#endif
        public List<GameObject> Objects;
        public List<int> ObjectIndecies;
    }

    [Serializable]
    public class VisibilityModifier
    {
        public string Name;
        public int Id;
        public bool DefaultValue;
#if !UNITY
        [Newtonsoft.Json.JsonIgnore]
#endif
        public List<GameObject> Objects;
        public List<int> ObjectIndecies;
    }
}