using System;
using SaberFactory2.Helpers;
using UnityEngine;

namespace SaberFactory2.Gizmo
{
    internal class ControllerInteractionHandler
    {
        public Vector3 Position => _controller.position;
        private readonly VRController _controller;
        private bool _wasPressed;
        public ControllerInteractionHandler(VRController controller)
        {
            _controller = controller;
        }

        public bool IsPressed()
        {
            return _controller.triggerValue > 0.5f;
        }

        public bool IsNear(Vector3 pos, float thresh)
        {
            return Vector3.Distance(Position, pos) < thresh;
        }

        public bool IsNearAndPressed(Vector3 pos, float thresh)
        {
            return IsNear(pos, thresh) && IsPressed();
        }

        public bool TriggerChangedThisFrame(out bool isPressed)
        {
            isPressed = false;
            var pressed = IsPressed();
            if (!_wasPressed && pressed)
            {
                _wasPressed = true;
                isPressed = true;
                return true;
            }
            if (_wasPressed && !pressed)
            {
                _wasPressed = false;
                isPressed = false;
                return true;
            }
            return false;
        }
    }

    internal interface IFactoryGizmo
    {
        public Color Color { get; }
        public Vector3 GetDelta(Vector3 newPos);
        public void Draw(Vector3 pos, Quaternion? rotation);
        public void Hover();
        public void Unhover();
    }

    internal abstract class FactoryDragGizmoBase : IFactoryGizmo
    {
        public Color Color => IsHovered || IsActive ? (CustomColor ?? GizmoColor).ColorWithAlpha(0.7f) : CustomColor ?? GizmoColor;
        protected abstract Color GizmoColor { get; }
        public Color? CustomColor { get; set; }
        protected abstract Mesh GizmoMesh { get; }
        protected bool IsHovered;
        protected bool IsActive;
        protected Action<Vector3> _pollFunction;
        private Vector3? _lastPos;
        public virtual void Init()
        {
            _lastPos = null;
        }

        public virtual Vector3 GetDelta(Vector3 newPos)
        {
            _lastPos ??= newPos;
            var lastPos = _lastPos.Value;
            _lastPos = newPos;
            return newPos - lastPos;
        }

        public void Draw(Vector3 pos, Quaternion? rotation = null)
        {
            rotation ??= Quaternion.identity;
            if (GizmoMesh)
            {
                GizmoDrawer.Draw(GizmoMesh, Color, pos, rotation.Value, Vector3.one * 0.1f);
                return;
            }
            GizmoDrawer.DrawSphere(pos, 0.05f, Color);
        }

        public void Hover()
        {
            IsHovered = true;
        }

        public void Unhover()
        {
            IsHovered = false;
        }

        public void Activate()
        {
            Init();
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void SetPollFunction(Action<Vector3> pollFunction)
        {
            _pollFunction = pollFunction;
        }

        public void Update(Vector3 newPos)
        {
            if (_pollFunction == null || !IsActive)
            {
                return;
            }
            _pollFunction.Invoke(GetDelta(newPos));
        }
    }

    internal class PositionGizmo : FactoryDragGizmoBase
    {
        public static Mesh PositionMesh;
        protected override Color GizmoColor => Color.green.ColorWithAlpha(0.1f);
        protected override Mesh GizmoMesh => PositionMesh;
    }

    internal class RotationGizmo : FactoryDragGizmoBase
    {
        public static Mesh RotationMesh;
        protected override Color GizmoColor => Color.cyan.ColorWithAlpha(0.1f);
        protected override Mesh GizmoMesh => RotationMesh;
    }

    internal class ScaleGizmo : FactoryDragGizmoBase
    {
        public static Mesh ScalingMesh;
        protected override Color GizmoColor => Color.red.ColorWithAlpha(0.1f);
        protected override Mesh GizmoMesh => ScalingMesh;
    }
}