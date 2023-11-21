﻿using System;
using System.Drawing;
using System.Numerics;

namespace BrewLib.Graphics.Cameras
{
    public class CameraPerspective : CameraBase
    {
        float fieldOfView;
        public float FieldOfView
        {
            get => fieldOfView;
            set
            {
                if (fieldOfView == value) return;
                fieldOfView = value;
                Invalidate();
            }
        }
        public CameraPerspective()
        {
            FieldOfView = 67;
            NearPlane = .001f;
            FarPlane = 1000;
        }

        public float NearPlaneHeight => Viewport.Height / (float)(2 * Math.Tan(FieldOfView / 2 * Math.PI / 180));

        // XXX This is supposed to be correct but doesn't work?
        // public float NearPlaneHeight2 => NearPlane * 2 * (float)Math.Tan(fieldOfView / 2 * Math.PI / 180);

        protected override void Recalculate(out Matrix4x4 view, out Matrix4x4 projection, out Rectangle internalViewport, out Rectangle extendedViewport)
        {
            var screenViewport = Viewport;
            var fovRadians = (float)Math.Max(.0001, Math.Min(fieldOfView * Math.PI / 180, Math.PI - .0001));
            var aspect = (float)screenViewport.Width / screenViewport.Height;

            internalViewport = extendedViewport = screenViewport;
            projection = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspect, NearPlane, FarPlane);
            view = Matrix4x4.CreateLookAt(Position, Position + Forward, Up);
        }
    }
}