﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
//using Rhino.Geometry;
using System.Numerics;
//using Grasshopper.Documentation;
//using Rhino;
//using Rhino.Collections;
using Quaternion = System.Numerics.Quaternion;

using Autodesk.DesignScript.Runtime;
using Autodesk.DesignScript.Interfaces;
using Autodesk.DesignScript.Geometry;

namespace ViveTrack
{
    [IsVisibleInDynamoLibrary(false)]
    public class VrTrackedDevice
    {
        public string device_class;
        private uint index;
        private CVRSystem vr;
        private VrTrackedDevices TrackedDevices;
        public string Battery { get { return GetStringProperty(ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float); } }
        public string ModelNumber { get { return GetStringProperty(ETrackedDeviceProperty.Prop_ModelNumber_String); } }
        public string SerialNumber { get { return GetStringProperty(ETrackedDeviceProperty.Prop_SerialNumber_String); } }
        public HmdMatrix34_t Pose;
        //public Transform CorrectedMatrix4X4;
        //public Vector3d Translation;
        public Quaternion Quaternion;
        //public Vector3d CorrectedTranslation;
        public Quaternion CorrectedQuaternion;

        public Vector3 Translation;
        public Vector3 CorrectedTranslation;
        public Matrix4x4 CorrectedMatrix4x4;
        

        internal List<object> TriggerStates;
        internal bool TriggerClicked;
        internal bool TriggerPressed;
        internal double TriggerValue;
        internal List<object> TouchPadStates;
        internal bool TouchPadTouched;
        internal bool TouchPadClicked;
        internal double TouchPadValueX;
        internal double TouchPadValueY;



        public VrTrackedDevice(VrTrackedDevices trackedDevices, int iindex)
        {
            this.vr = trackedDevices.vr;
            this.TrackedDevices = trackedDevices;
            this.index = Convert.ToUInt32(iindex);
            this.device_class = GetClass();
            this.TriggerPressed = false;

        }

        public VrTrackedDevice(){}

        string GetStringProperty(ETrackedDeviceProperty prop)
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            var capactiy = vr.GetStringTrackedDeviceProperty(this.index, prop, null, 0, ref error);
            if (capactiy > 1)
            {
                var result = new System.Text.StringBuilder((int)capactiy);
                vr.GetStringTrackedDeviceProperty(this.index, prop, result, capactiy, ref error);
                return result.ToString();
            }
            return (error != ETrackedPropertyError.TrackedProp_Success) ? error.ToString() : "<unknown>";
        }

        public string GetTrackedDeviceString()
        {
            var error = ETrackedPropertyError.TrackedProp_Success;
            var capacity = vr.GetStringTrackedDeviceProperty(this.index, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, null, 0, ref error);
            if (capacity > 1)
            {
                var result = new System.Text.StringBuilder((int)capacity);
                vr.GetStringTrackedDeviceProperty(this.index, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, result, capacity, ref error);
                return result.ToString();
            }
            return null;
        }


        private string GetClass()
        {
            var type = vr.GetTrackedDeviceClass(this.index);
            if (type == ETrackedDeviceClass.Controller)
            {
                this.TrackedDevices.Controllers += 1;
                this.TrackedDevices.IndexesByClasses["Controller"].Add(Convert.ToInt16(index));
                return "Controller";
            }
            if (type == ETrackedDeviceClass.HMD)
            {
                this.TrackedDevices.HMDs += 1;
                this.TrackedDevices.IndexesByClasses["HMD"].Add(Convert.ToInt16(index));
                return "HMD";
            }
            if (type == ETrackedDeviceClass.Tracker)
            {
                this.TrackedDevices.Trackers += 1;
                this.TrackedDevices.IndexesByClasses["Tracker"].Add(Convert.ToInt16(index));
                return "Tracker";
            }
            if (type == ETrackedDeviceClass.TrackingReference)
            {
                this.TrackedDevices.TrackingReferences += 1;
                this.TrackedDevices.IndexesByClasses["Lighthouse"].Add(Convert.ToInt16(index));
                return "Lighthouse";
            }
            return "unknown";
        }

        public void ConvertPose()
        {
            GetTranslationFromPose();
            GetQuaternionFromPose();
            GetCorrectedTranslation();
            GetCorrectedQuaternion();
            GetCorrectedMatrix4X4();
        }

        public void GetTranslationFromPose()
        {
            this.Translation = new Vector3(Pose.m3, Pose.m7, Pose.m11);
        }

        public void GetQuaternionFromPose()
        {
            var w = Math.Sqrt(Math.Max(0, 1 + Pose.m0 + Pose.m5 + Pose.m10)) / 2;
            var x = Math.Sqrt(Math.Max(0, 1 + Pose.m0 - Pose.m5 - Pose.m10)) / 2;
            var y = Math.Sqrt(Math.Max(0, 1 - Pose.m0 + Pose.m5 - Pose.m10)) / 2;
            var z = Math.Sqrt(Math.Max(0, 1 - Pose.m0 - Pose.m5 + Pose.m10)) / 2;
            x = Math.Abs(x) * Math.Sign(Pose.m9 - Pose.m6);
            y = Math.Abs(y) * Math.Sign(Pose.m2 - Pose.m8);
            z = Math.Abs(z) * Math.Sign(Pose.m4 - Pose.m1);
            this.Quaternion = new Quaternion((float)x, (float)y, (float)z, (float)w);
        }

        public void GetCorrectedTranslation()
        {
            this.CorrectedTranslation = new Vector3(Translation.X, -Translation.Z, Translation.Y);
        }

        public void GetCorrectedQuaternion()
        {
            this.CorrectedQuaternion = new Quaternion(Quaternion.X, -Quaternion.Z, Quaternion.Y, Quaternion.W);
        }

        public void GetCorrectedMatrix4X4()
        {
            Matrix4x4 translationMatrix = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(new Vector3(CorrectedTranslation.X, CorrectedTranslation.Y, CorrectedTranslation.Z)));
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(CorrectedQuaternion);
            rotationMatrix = Matrix4x4.Transpose(rotationMatrix);
            //Matrix4x4 multiply = Matrix4x4.Multiply(translationMatrix, rotationMatrix);
            //CorrectedMatrix4X4 = ConvertFromSystemMatrixToRhinoMatrix(multiply);
            this.CorrectedMatrix4x4 = Matrix4x4.Multiply(translationMatrix, rotationMatrix);

            ////Transform the matrix by calibration matrix
            //CorrectedMatrix4X4 = Transform.Multiply(StartVive.CalibrationTransform, CorrectedMatrix4X4);
            //this.CorrectedMatrix4x4 = Matrix4x4.Multiply(Start.CalibrationTransform, this.CorrectedMatrix4x4);  // @TODO: review if this is the correct multiplication order...
        }

        public void CorrectGenericTrackerMatrix()
        {
            //Matrix4x4 oldMatrix = ConvertFromRhinoMatrixToSystemMatrix(new Matrix(CorrectedMatrix4X4));
            Matrix4x4 transformMatrix4X4 = new Matrix4x4(-1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1);

            //Matrix4x4 newMatrix4X4 = Matrix4x4.Multiply(oldMatrix, transformMatrix4X4);
            //CorrectedMatrix4X4 = ConvertFromSystemMatrixToRhinoMatrix(newMatrix4X4);
            CorrectedMatrix4x4 = Matrix4x4.Multiply(CorrectedMatrix4x4, transformMatrix4X4);
        }

        //public static Matrix4x4 ConvertFromRhinoMatrixToSystemMatrix(Matrix rMatrix)
        //{
        //    Matrix4x4 sm = new Matrix4x4(
        //        (float)rMatrix[0, 0], (float)rMatrix[0, 1], (float)rMatrix[0, 2], (float)rMatrix[0, 3],
        //        (float)rMatrix[1, 0], (float)rMatrix[1, 1], (float)rMatrix[1, 2], (float)rMatrix[1, 3],
        //        (float)rMatrix[2, 0], (float)rMatrix[2, 1], (float)rMatrix[2, 2], (float)rMatrix[2, 3],
        //        (float)rMatrix[3, 0], (float)rMatrix[3, 1], (float)rMatrix[3, 2], (float)rMatrix[3, 3]);
        //    return sm;
        //}

        //public Transform ConvertFromSystemMatrixToRhinoMatrix(Matrix4x4 m)
        //{
        //    Transform t = new Transform
        //    {
        //        M00 = m.M11,
        //        M01 = m.M12,
        //        M02 = m.M13,
        //        M03 = m.M14,
        //        M10 = m.M21,
        //        M11 = m.M22,
        //        M12 = m.M23,
        //        M13 = m.M24,
        //        M20 = m.M31,
        //        M21 = m.M32,
        //        M22 = m.M33,
        //        M23 = m.M34,
        //        M30 = m.M41,
        //        M31 = m.M42,
        //        M32 = m.M43,
        //        M33 = m.M44
        //    };
        //    return t;
        //}









        public override string ToString()
        {
            return "Name: " + device_class + ",Model: " + ModelNumber + ",Serial: " +  SerialNumber + ",Battery: " + Battery;
        }

        public string PosetoString()
        {
            return $"{Pose.m0},{Pose.m1},{Pose.m2},{Pose.m3},{Pose.m4},{Pose.m5},{Pose.m6},{Pose.m7},{Pose.m8},{Pose.m9},{Pose.m10},{Pose.m11}";

        }

        public void GetControllerTriggerState()
        {
            VRControllerState_t controllerState = new VRControllerState_t();

            this.vr.GetControllerState(this.index, ref controllerState);
            TriggerPressed = controllerState.ulButtonTouched == 8589934592;
            TriggerValue = controllerState.rAxis1.x;
            TriggerClicked= controllerState.ulButtonPressed == 8589934592 && Math.Abs(TriggerValue - 1) < 0.001;

            TouchPadTouched = controllerState.ulButtonTouched == 4294967296;
            TouchPadClicked = controllerState.ulButtonPressed == 4294967296;
            TouchPadValueX = controllerState.rAxis0.x;
            TouchPadValueY = controllerState.rAxis0.y;

            AddStatesToLists();

        }

        internal void AddStatesToLists()
        {
            TriggerStates = new List<object>(){TriggerPressed, TriggerClicked, TriggerValue};
            TouchPadStates = new List<object>(){ TouchPadTouched, TouchPadClicked, TouchPadValueX, TouchPadValueY };
        }

    }
    //rAxis1 x trigger from 0.019 to 1
    //rAxis0 x -1 to 1 horizontal left to right y -1 to 1 verical bottom to up
    //button 4294967296
    //trigger 8589934592


}

