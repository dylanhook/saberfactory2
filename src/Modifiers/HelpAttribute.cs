using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SaberFactory2.Modifiers
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(HelpAttribute))]
    public class HelpDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var helpAttribute = attribute as HelpAttribute;
            if (helpAttribute == null) return base.GetPropertyHeight(property, label);

            var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            var content = new GUIContent(helpAttribute.text);
            return style.CalcHeight(content, EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth - 20) + 10;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var helpAttribute = attribute as HelpAttribute;
            if (helpAttribute == null) return;

            var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 14, alignment = TextAnchor.MiddleLeft };
            EditorGUI.HelpBox(position, helpAttribute.text, UnityEditor.MessageType.Info);
        }
    }
#else
    public enum MessageType
    {
        None,
        Info,
        Warning,
        Error,
    }
#endif

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public class HelpAttribute : PropertyAttribute
    {
        public readonly string text;
        public readonly MessageType type;

        public HelpAttribute(string text, MessageType type = MessageType.Info)
        {
            this.text = text;
            this.type = type;
        }
    }
}