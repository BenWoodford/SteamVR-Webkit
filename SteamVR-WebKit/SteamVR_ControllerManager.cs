//========= Copyright 2015, Valve Corporation, All rights reserved. ===========
//
// Purpose: Enables/disables left and right controller objects based on
// connectivity and relative positions.
//
//=============================================================================

using System;

namespace Valve.VR
{
    public class SteamVR_ControllerManager : System.IDisposable
    {
        public SteamVR_Controller left = new SteamVR_Controller("left");
        public SteamVR_Controller right = new SteamVR_Controller("right");
        public SteamVR_Controller[] objects; // populate with objects you want to assign to additional controllers

        public uint[] indices; // assigned
        public bool[] connected = new bool[OpenVR.k_unMaxTrackedDeviceCount]; // controllers only

        // cached roles - may or may not be connected
        uint leftIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
        uint rightIndex = OpenVR.k_unTrackedDeviceIndexInvalid;
        
        public SteamVR_ControllerManager()
        {
            // Add left and right entries to the head of the list so we only have to operate on the list itself.
            var additional = (this.objects != null) ? this.objects.Length : 0;
            var objects = new SteamVR_Controller[2 + additional];
            indices = new uint[2 + additional];
            objects[0] = right;
            indices[0] = OpenVR.k_unTrackedDeviceIndexInvalid;
            objects[1] = left;
            indices[1] = OpenVR.k_unTrackedDeviceIndexInvalid;
            for (int i = 0; i < additional; i++)
            {
                objects[2 + i] = this.objects[i];
                indices[2 + i] = OpenVR.k_unTrackedDeviceIndexInvalid;
            }
            this.objects = objects;

            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj != null)
                    obj.SetActive(false);
            }

            OnTrackedDeviceRoleChanged();

            for (int i = 0; i < SteamVR.connected.Length; i++)
                if (SteamVR.connected[i])
                    OnDeviceConnected(i, true);

            SteamVR_Event.Listen("input_focus", OnInputFocus);
            SteamVR_Event.Listen("device_connected", OnDeviceConnected);
            SteamVR_Event.Listen("TrackedDeviceRoleChanged", OnTrackedDeviceRoleChanged);
        }
        
        static string[] labels = { "left", "right" };
        
        // Hide controllers when the dashboard is up.
        private void OnInputFocus(params object[] args)
        {
            bool hasFocus = (bool)args[0];
            if (hasFocus)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    var obj = objects[i];
                    if (obj != null)
                    {
                        var label = (i < 2) ? labels[i] : (i - 1).ToString();
                        //ShowObject(obj.transform, "hidden (" + label + ")");
                        Console.WriteLine("Controller manager wants to show the " + label + " controller.");
                    }
                }
            }
            else
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    var obj = objects[i];
                    if (obj != null)
                    {
                        var label = (i < 2) ? labels[i] : (i - 1).ToString();
                        //HideObject(obj.transform, "hidden (" + label + ")");
                        Console.WriteLine("Controller manager wants to hide the " + label + " controller.");
                    }
                }
            }
        }
        
        /*// Reparents to a new object and deactivates that object (this allows
        // us to call SetActive in OnDeviceConnected independently.
        private void HideObject(Transform t, string name)
        {
            var hidden = new GameObject(name).transform;
            hidden.parent = t.parent;
            t.parent = hidden;
            hidden.gameObject.SetActive(false);
        }
        private void ShowObject(Transform t, string name)
        {
            var hidden = t.parent;
            if (hidden.gameObject.name != name)
                return;
            t.parent = hidden.parent;
            Destroy(hidden.gameObject);
        }*/

        private void SetTrackedDeviceIndex(int objectIndex, uint trackedDeviceIndex)
        {
            // First make sure no one else is already using this index.
            if (trackedDeviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (i != objectIndex && indices[i] == trackedDeviceIndex)
                    {
                        var obj = objects[i];
                        if (obj != null)
                            obj.SetActive(false);

                        indices[i] = OpenVR.k_unTrackedDeviceIndexInvalid;
                    }
                }
            }

            // Only set when changed.
            if (trackedDeviceIndex != indices[objectIndex])
            {
                indices[objectIndex] = trackedDeviceIndex;

                var obj = objects[objectIndex];
                if (obj != null)
                {
                    if (trackedDeviceIndex == OpenVR.k_unTrackedDeviceIndexInvalid)
                        obj.SetActive(false);
                    else
                    {
                        obj.SetActive(true);
                        //obj.BroadcastMessage("SetDeviceIndex", (int)trackedDeviceIndex, SendMessageOptions.DontRequireReceiver);
                        Console.WriteLine("obj.BroadcastMessage(\"SetDeviceIndex\", (int)trackedDeviceIndex="+ trackedDeviceIndex + ", SendMessageOptions.DontRequireReceiver);");
                    }
                }
            }
        }

        // Keep track of assigned roles.
        private void OnTrackedDeviceRoleChanged(params object[] args)
        {
            Console.WriteLine("Controller role change detected");
            Refresh();
        }

        // Keep track of connected controller indices.
        private void OnDeviceConnected(params object[] args)
        {
            var index = (uint)(int)args[0];
            bool changed = this.connected[index];
            this.connected[index] = false;

            var connected = (bool)args[1];
            if (connected)
            {
                var system = OpenVR.System;
                if (system != null && system.GetTrackedDeviceClass(index) == ETrackedDeviceClass.Controller)
                {
                    this.connected[index] = true;
                    changed = !changed; // if we clear and set the same index, nothing has changed
                }
            }

            if (changed)
                Refresh();
        }

        private void Refresh()
        {
            int objectIndex = 0;

            var system = OpenVR.System;
            if (system != null)
            {
                leftIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                rightIndex = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
            }

            // If neither role has been assigned yet, try hooking up at least the right controller.
            if (leftIndex == OpenVR.k_unTrackedDeviceIndexInvalid && rightIndex == OpenVR.k_unTrackedDeviceIndexInvalid)
            {
                for (uint deviceIndex = 0; deviceIndex < connected.Length; deviceIndex++)
                {
                    if (connected[deviceIndex])
                    {
                        SetTrackedDeviceIndex(objectIndex++, deviceIndex);
                        break;
                    }
                }
            }
            else
            {
                SetTrackedDeviceIndex(objectIndex++, (rightIndex < connected.Length && connected[rightIndex]) ? rightIndex : OpenVR.k_unTrackedDeviceIndexInvalid);
                SetTrackedDeviceIndex(objectIndex++, (leftIndex < connected.Length && connected[leftIndex]) ? leftIndex : OpenVR.k_unTrackedDeviceIndexInvalid);

                // Assign out any additional controllers only after both left and right have been assigned.
                if (leftIndex != OpenVR.k_unTrackedDeviceIndexInvalid && rightIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
                {
                    for (uint deviceIndex = 0; deviceIndex < connected.Length; deviceIndex++)
                    {
                        if (objectIndex >= objects.Length)
                            break;

                        if (!connected[deviceIndex])
                            continue;

                        if (deviceIndex != leftIndex && deviceIndex != rightIndex)
                        {
                            SetTrackedDeviceIndex(objectIndex++, deviceIndex);
                        }
                    }
                }
            }

            // Reset the rest.
            while (objectIndex < objects.Length)
            {
                SetTrackedDeviceIndex(objectIndex++, OpenVR.k_unTrackedDeviceIndexInvalid);
            }
        }

        ~SteamVR_ControllerManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            SteamVR_Event.Remove("input_focus", OnInputFocus);
            SteamVR_Event.Remove("device_connected", OnDeviceConnected);
            SteamVR_Event.Remove("TrackedDeviceRoleChanged", OnTrackedDeviceRoleChanged);
        }
    }
}
