using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.DirectX;

using NovodexWrapper;

namespace Simulator
{
    class Parts
    {
        NxFixedJointDesc F;

        NxScene scene;
        List<NxJoint> joints;
        List<NxActor> actors;

        NxActor body;

        public Parts(NxScene s, List<NxJoint> j, List<NxActor> a)
        {
            scene = s;
            joints = j;
            actors = a;
        }

        public NxActor getBodyActor()
        {
            return body;
        }

        public NxActor CreateFoot(Vector3 pos, Servo sn)
        {
            Vector3 boxDim = new Vector3(0.9f, 0.1f, 1.4f);
            float density = 10.0f;

            NxActorDesc actorDesc = new NxActorDesc();
            NxBodyDesc bodyDesc = new NxBodyDesc();

            NxBoxShapeDesc boxDesc = new NxBoxShapeDesc();
            boxDesc.dimensions = boxDim;
            boxDesc.localPose = NxMat34.Translation(new Microsoft.DirectX.Vector3(0, boxDim.Y, 0));
            actorDesc.addShapeDesc(boxDesc);

            actorDesc.BodyDesc = bodyDesc;
            actorDesc.density = density;
            actorDesc.globalPose = NxMat34.Translation(pos);

            NxActor n = scene.createActor(actorDesc);

            createJoint(sn.getActor(), n);

            n.UserData = new IntPtr(4);

            actors.Add(n);

            return n;
        }

        public NxActor CreateHand(Vector3 pos, Servo sn, int ax)
        {
            NxActorDesc actorDesc = new NxActorDesc();
            NxBodyDesc bodyDesc = new NxBodyDesc();

            Vector3 boxDim = new Vector3(0.3f, 0.3f, 0.3f);

            NxBoxShapeDesc boxDesc = new NxBoxShapeDesc();
            boxDesc.dimensions = boxDim;
            boxDesc.localPose = Matrix.Translation(new Vector3(0, -boxDim.Y, 0));
            actorDesc.addShapeDesc(boxDesc);

            actorDesc.BodyDesc = bodyDesc;
            actorDesc.density = 1.0f;
            actorDesc.globalPose = Matrix.Translation(pos);

            NxActor n = scene.createActor(actorDesc);

            if (ax == 0)
            {
                //Left hand
                n.UserData = new IntPtr(3);
            }
            else
            {
                //right hand
                n.UserData = new IntPtr(2);
            }
            sn.Connect(n);

            actors.Add(n);
            return n;
        }


        public NxActor CreateBody(Vector3 pos, Servo s1, Servo s2, Servo s3, Servo s4)
        {
            Vector3 boxDim = new Vector3(1f, 1f, 0.5f);
            float density = 1.0f;

            NxActorDesc actorDesc = new NxActorDesc();
            NxBodyDesc bodyDesc = new NxBodyDesc();
            NxBoxShapeDesc boxDesc = new NxBoxShapeDesc();

            boxDesc.dimensions = boxDim;
            boxDesc.localPose = Matrix.Translation(new Vector3(0, 0, 0));
            actorDesc.addShapeDesc(boxDesc);

            actorDesc.BodyDesc = bodyDesc;
            actorDesc.density = density;
            actorDesc.globalPose = Matrix.Translation(pos);

            NxActor n = scene.createActor(actorDesc);

            createJoint(s1.getActor(), n);
            createJoint(s2.getActor(), n);
            createJoint(s3.getActor(), n);
            createJoint(s4.getActor(), n);

            n.UserData = new IntPtr(5);

            body = n;

            actors.Add(body);
            return n;
        }

        public NxActor CreateKnee(Vector3 pos, NxActor a1, NxActor a2)
        {
            Vector3 boxDim = new Vector3(0.1f, 0.1f, 0.1f);
            float density = 1;

            NxActorDesc actorDesc = new NxActorDesc();
            NxBodyDesc bodyDesc = new NxBodyDesc();

            NxBoxShapeDesc boxDesc = new NxBoxShapeDesc();
            boxDesc.dimensions = boxDim;
            actorDesc.addShapeDesc(boxDesc);

            actorDesc.BodyDesc = bodyDesc;
            actorDesc.density = density;
            actorDesc.globalPose = Matrix.Translation(pos);

            NxActor n = scene.createActor(actorDesc);

            createJoint(n, a1);
            createJoint(n, a2);

            n.UserData = new IntPtr(6);
            actors.Add(n);
            return n;
        }


        public void createJoint(NxActor a1, NxActor a2)
        {
            F = new NxFixedJointDesc();
            F.actor[0] = a1;
            F.actor[1] = a2;

            NxJoint t = scene.createJoint(F);
            joints.Add(t);
        }

        public void createJoint(Servo s1, Servo s2, int joint_type)
        {
            switch (joint_type)
            {
                case 0:
                    // fixed joint
                    createJoint(s1.getActorB(), s2.getActorB());
                    break;
                case 1:
                    //rotating joint s1 = driver
                    s1.ConnectC(s2.getActorB());
                    break;
            }
        }

    }
}
