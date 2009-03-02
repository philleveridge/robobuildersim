using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using NovodexWrapper;
using Microsoft.DirectX;

namespace Simulator
{
    class Servo2
    {
        public Vector3 pos;

        int id;
        NxActor actorTop;
        NxActor actorMid;
        NxActor actorEnd;
        Matrix localpose;

        NxScene scene;
        List<NxActor> actors;
        List<NxJoint> joints;

        float steerAngle = 0;

        NxRevoluteJoint joint;

        int angle = 0;
        bool StandardRes = true;  // 0-269, otherwise High res 0-333

        public Servo2(string n, Vector3 pos, float y, float p, float r, NxScene s, List<NxActor> l, List<NxJoint> j)
        {
            this.pos = pos;
            id = Int32.Parse(n);
            scene = s;
            actors = l;
            joints = j;
            this.Create(pos, y, p, r);
        }

        public NxActor getActor()
        {
            return actorTop;
        }

        public NxActor getActorB()
        {
            return actorEnd;
        }

        public void Create(Vector3 pos, float y, float p, float r)
        {
            NxActor actor;
            NxActorDesc actorDesc;
            NxBodyDesc bodyDesc;

            NxCapsuleShapeDesc capDesc;

            float density = 1.0f;
            Vector3 boxes = new Vector3(0.6f, 0.3f, 0.45f);
            Vector3 rot = new Vector3(0, boxes.Y / 2, boxes.Z / 2);

            actorDesc = new NxActorDesc();
            bodyDesc = new NxBodyDesc();

            capDesc = new NxCapsuleShapeDesc();

            capDesc.radius = 1.0f; 
            capDesc.height = 1.0f;

            localpose = NxMat34.RotationYawPitchRoll(y * NovodexUtil.DEG_TO_RAD, p * NovodexUtil.DEG_TO_RAD, r * NovodexUtil.DEG_TO_RAD); //Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(y), MathHelper.ToRadians(p), MathHelper.ToRadians(r));
            capDesc.localPose = localpose;

            actorDesc.addShapeDesc(capDesc);

            actorDesc.BodyDesc= bodyDesc;
            actorDesc.density = density;
            actorDesc.globalPose = NxMat34.Translation(pos); //Matrix.CreateTranslation(pos);

            actor = scene.createActor(actorDesc);
            actor.Name="Servo" + id;

            actor.UserData = new IntPtr(100+id);

            actors.Add(actor);

            actorTop = actor;
            actorEnd = actor;
            return;
        }

        public void Connect(NxActor a)
        {
            NxRevoluteJointDesc R = new NxRevoluteJointDesc();
            R.actor[0] = actorTop;
            R.actor[1] = a;

            //Reference point that the axis passes through.
            Matrix n =actorTop.getGlobalPose();

            //n *= Matrix.Translation(new Vector3(1, 0, 0));

            R.setGlobalAnchor(NovodexUtil.getMatrixPos(ref n));

            //The direction of the axis the bodies revolve around.    

            R.setGlobalAxis(NovodexUtil.getMatrixZaxis(ref localpose));  //forward ???



            joint = (NxRevoluteJoint)scene.createJoint(R);
            joints.Add(joint);

            turn(0);
        }

        public NxRevoluteJoint getJoint()
        {
            return joint;
        }

        public String getTurn()
        {
            return steerAngle.ToString();
        }

        public void setPos(int pos)
        {
            if (pos < 0) pos = 0;
            if (pos > 269) pos = 269;

            angle = pos-135;

            setAngle(angle);
        }

        public int getPos()
        {
            return angle + 135;
        }

        public void turn(float angle)
        {
            //selectedActor.
            float maxSteerAngle = 30.0f;

            steerAngle += angle;
            float sangle;

            if (steerAngle > 0.0)
                sangle = Math.Min(steerAngle, maxSteerAngle);
            else
                sangle = Math.Max(steerAngle, -maxSteerAngle);

            setAngle(sangle);
        }

        private void setAngle(float ang)
        {
            steerAngle = ang;
            NxSpringDesc ns = new NxSpringDesc(50000, 5000, ang * (float)Math.PI / 180);
            if (joint != null) joint.setSpring(ns);
        }
    }
}
