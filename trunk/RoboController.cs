using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

namespace Simulator
{
    class RoboController
    {
        string MotionZeroPos = "Zero:125,201,163,67,108,125,48,89,184,142,89,39,124,162,211,127";
        string HunoBasicPose = "Basic:125,179,199,88,108,126,72,49,163,141,51,47,49,199,205,205";

        string TestAction  = "test2:8:Uha ha..:0:1:4:2:0:5:"
                + "ST000:10:Basic Pose:2:0:0:0:0:0:1:7:0:0:0:1:5:"
                + "ST001:14:standup type A:2:5:63:31:31:234:1:1:0:0:0:2:5:"
                + "ST002:14:standup type B:2:5:63:31:31:98:1:2:0:0:0:3:5:"
                + "ST003:12:goto index 1:2:0:0:0:0:0:4:0:1:0:0:";

        Servo[] servos;
        Hashtable motions;
        Hashtable actions;

        //accelerometer  (tbd)
        float dx;
        float dy;
        float dz;

        //Postion sensor  (tbd)
        float psd_val;  

        public bool runningflag = false;
        public int scene_number = 0;
        public int frame_number = 0;

        int max_scene;
        int max_frame;
        int[] frame;            // delta
        int frames_ms = 100;
        string current_motion;

        DateTime lastupdate;

        public RoboController()
        {
            lastupdate = DateTime.Now;
            motions = new Hashtable();
            actions = new Hashtable();

            current_motion = "";

            // load default motions

            setMotions(MotionZeroPos);
            setMotions(HunoBasicPose);

            setAction(TestAction);

            servos = null;
        }

        public void setAccel(float x, float y, float z)
        {
            dx = x; dy = y; dz = z;
        }

        public void setServo(Servo[] s)
        {
            servos = s;
        }

        public void setMZP()
        {
            playMotion("Zero", 10, 500);
        }

        public void setBasicPose()
        {
             playMotion("Basic", 10, 500);
        }

        public int setAction(string psv)
        {
            string[] ar = psv.Split(':');
            motions[ar[0]] = psv;
            return 0;
        }

        public int startAction(string an)
        {
            string action = actions[an].ToString();
            return 0;
        }

        public int doNextAction(string an)
        {
            return 0;
        }

        public int stopAction(string an)
        {
            return 0;
        }

        public int setMotions(string psv)
        {
            int n = motions.Count;
            int r = 0;

            string ns = "Prog" + (n + 1);

            int pi = psv.IndexOf(":");

            if (pi > 0)
            {
                ns = psv.Substring(0, pi);
                psv = psv.Substring(pi + 1);
            }
            if (motions.ContainsKey(ns)) r = 1;
            motions[ns] = psv;
            return r;
        }

        public int[] getMotions(string mn, int sn)
        {
            Console.WriteLine("gM Debug: " + mn + ": "+ sn);

            int[] marray = new int[16];

            if (!motions.ContainsKey(mn))
                return null;

            string[] m = motions[mn].ToString().Split(',');

            if (sn * 16 > m.Length)
            {
                return null;
            }

            for (int i = 0; i < 16; i++)
            {
                if (sn * 16 + i < m.Length)
                    Int32.TryParse(m[sn * 16 + i], out marray[i]);
                else
                    marray[i] = 135; // mid position

                Console.Write(marray[i] + ", ");
            }
            Console.WriteLine("");

            return marray;
        }

        public void setPos(int[] ids)
        {
            for (int i = 0; i < 16; i++)
            {
                servos[i].setPos(ids[i]);
            }
        }

        public void setPos(int id, int v)
        {
               if (servos[id] != null) servos[id].setPos(v);
        }

        public int getPos(int id)
        {
            return (servos[id] == null) ? 0 : servos[id].getPos();
        }

        public void playMotion(string mname, int mf, int fps)
        {
            //Playmotion "name" Duration frames.

            Console.WriteLine("pM: " + mname + ": " + mf + ":" + fps);

            frames_ms = fps / mf;

            current_motion = mname;

            frame_number = 0;
            scene_number = 0;

            max_frame = mf;

            frame = new int[16];

            int[] pos = getMotions(mname, 0);

            if (pos == null)
            {
                Console.WriteLine("Motion doesn't exist - " + mname);
                current_motion = "";
                return;
            }

            if (servos == null)
            {
                Console.WriteLine("servos must configured first " );
                current_motion = "";
                return;
            }

            string[] temp = motions[mname].ToString().Split(',');
            max_scene = temp.Length / 16;

            for (int i = 0; i<16 ; i++)
            {
                frame[i] = (pos[i] - servos[i].getPos()) / max_frame;
                Console.Write(frame[i] + ", ");
            }

            runningflag = true;
            lastupdate = DateTime.Now;
        }

        public bool updateMotion()
        {
            if (!runningflag || current_motion=="") return false;

            if ((DateTime.Now - lastupdate).TotalMilliseconds < frames_ms)
                return false;

            Console.WriteLine("uM time = " + (DateTime.Now - lastupdate).TotalMilliseconds);
            lastupdate = DateTime.Now;

            // if time elapsed move t next step
            frame_number++;

            if (frame_number >= max_frame)
            {
                frame_number = 0;
                scene_number++;

                int i;
                int[] opos = getMotions(current_motion, scene_number - 1);

                for (i = 0; i < 16; i++)
                {
                    servos[i].setPos(opos[i]);  //make sure servo gets to end of travel for that motion
                }

                if (scene_number < max_scene)
                {

                    // create next delta
                    int[] npos = getMotions(current_motion, scene_number);

                    for (i = 0; i < 16; i++)
                    {
                        frame[i] = (npos[i]-opos[i])/max_frame;
                        Console.Write(frame[i] + ", ");  //next delta
                    }
                    Console.WriteLine("");
                }
                else
                {
                    runningflag = false;
                    current_motion = "";
                }
            }
            else
            {
                // display next frame
                // add delta to current position
                Console.WriteLine("Play: " + current_motion + ": " + frame_number + ") " + scene_number);

                for (int i = 0; i < 16; i++)
                {
                    servos[i].setPos(servos[i].getPos() + frame[i]);
                }
            }
            return true;
        }

        public void dumpPos(Sim cp)
        {
            for (int i = 0; i < 16; i++)
            {
                Console.WriteLine("Servo " + i+ " = " + servos[i].getPos());
                cp.updateStatus(i.ToString(), servos[i].getPos().ToString());
            }
        }
    }
}
