﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Expansions.Serenity;
using Steamworks;
using UnityEngine;
using static KSP.UI.Screens.RDNode;
using static SoftMasking.SoftMask;

// effector (part)
// -> movingObject 1
//   -> ikjoint 1 (servo)
//     -> movingObject 2
//       -> ikjoint 2 (servo)

namespace EasyRobotics
{
    public class IKJoint4
    {
        public BasicTransform baseTransform;
        public BasicTransform movingTransform;
        public BaseServo servo;

        BaseField servoTargetAngle;
        BaseField servoSoftMinMaxAngles;

        public Vector3 axis; // local space
        public Vector3 perpendicularAxis;
        public bool rotateToDirection;

        private float ServoAngle
        {
            get => servoTargetAngle.GetValue<float>(servo);
            set => servoTargetAngle.SetValue(value, servo);
        }

        private Vector2 ServoMinMaxAngle => servoSoftMinMaxAngles.GetValue<Vector2>(servo);

        private static Material _servoMaterial;

        public static Material ServoMaterial
        {
            get
            {
                if (_servoMaterial == null)
                {
                    _servoMaterial = new Material(Shader.Find("KSP/Alpha/Translucent"));
                    _servoMaterial.SetColor("_Color", new Color(1f, 1f, 0f, 0.3f));
                }

                return _servoMaterial;
            }
        }

        public IKJoint4(BaseServo servo)
        {
            this.servo = servo;
            servoTargetAngle = servo.Fields["targetAngle"];
            servoSoftMinMaxAngles = servo.Fields["softMinMaxAngles"];
            axis = servo.GetMainAxis();
            perpendicularAxis = Perpendicular(axis);
            baseTransform = new BasicTransform(null);
            movingTransform = new BasicTransform(baseTransform, Vector3.zero, Quaternion.AngleAxis(ServoAngle, axis), true);
        }

        public void UpdateDirection(BasicTransform effector, Transform target)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            if (rotateToDirection)
            {
                // Point the effector along the target direction
                // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                // while other servos are matching target direction


                movingTransform.Rotation = Quaternion.FromToRotation(effector.Up, -target.up) * movingTransform.Rotation;
            }
            else
            {
                // Point the effector towards the target
                Vector3 targetPosition = target.position + target.up;
                Vector3 directionToEffector = effector.Position - baseTransform.Position;
                Vector3 directionToTarget = target.position - baseTransform.Position;
                Quaternion rotationOffset = Quaternion.FromToRotation(directionToEffector, directionToTarget);
                movingTransform.Rotation = rotationOffset * movingTransform.Rotation;
            }
        }

        public void ConstrainToAxis()
        {
            Vector3 from = movingTransform.Rotation * axis;
            Vector3 to = baseTransform.Rotation * axis;
            Quaternion correction = Quaternion.FromToRotation(from, to);
            movingTransform.Rotation = correction * movingTransform.Rotation;
        }

        public void ConstrainToMinMaxAngle()
        {
            Vector3 baseDir = perpendicularAxis;
            Vector3 newDir = movingTransform.LocalRotation * perpendicularAxis;
            float requestedAngle = Vector3.SignedAngle(baseDir, newDir, axis);

            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = Math.Max(minMax.x, -179.99f);
            float servoMaxAngle = Math.Min(minMax.y, 179.99f);

            if (requestedAngle < servoMinAngle)
            {
                requestedAngle = servoMinAngle;
                movingTransform.LocalRotation = Quaternion.AngleAxis(servoMinAngle, axis);
            }
            else if (requestedAngle > servoMaxAngle)
            {
                requestedAngle = servoMaxAngle;
                movingTransform.LocalRotation = Quaternion.AngleAxis(servoMaxAngle, axis);
            }

            ServoAngle = requestedAngle;
        }

        public void Evaluate(BasicTransform effector, Transform target)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            UpdateDirection(effector, target);
            ConstrainToAxis();
            ConstrainToMinMaxAngle();
        }

        private Vector3 Perpendicular(Vector3 vec)
        {
            if (Mathf.Abs(vec.x) > Mathf.Abs(vec.z))
                return new Vector3(-vec.y, vec.x, 0f);
            else
                return new Vector3(0f, -vec.z, vec.y);
        }
    }
}
