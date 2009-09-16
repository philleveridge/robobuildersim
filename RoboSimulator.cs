//By Jason Zelsnack

using System;
using System.IO;
using System.Windows.Forms;
using System.Collections;
using System.Drawing;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Collections.Generic;

using NovodexWrapper;

namespace Simulator
{
	public enum DriveObjectEnum{Camera,Unicycle,CapsuleController,BoxController,Car}


    public class Sim : System.Windows.Forms.Form
    {
        public bool reset;
        public bool setPressed;
        public bool runPressed;
        public bool stepPressed;
        public bool debugRender;
        public bool simulation;
        public bool simTrigger;

        public bool bodyvis;


        public const int MaxServos = 16;
        public const int MaxJoints = 99;

        string filename;

        int nextfree = 0;
        int current_servo = 0;
        float zoff = 0;
        NxActor selectedActor;
        NxScene scene;
        Vector3 com;

        protected List<NxJoint> joints = new List<NxJoint>();
        protected List<NxActor> actors = new List<NxActor>();

        RoboController rbc;
        simpleGA sga;
        IniManager IniData;
        Servo[] servos;
        Parts kit;
        DateTime rt;

        System.Windows.Forms.Label[] servoTag = new System.Windows.Forms.Label[16];
        System.Windows.Forms.HScrollBar[] servoValue = new System.Windows.Forms.HScrollBar[16];
        System.Windows.Forms.CheckBox[] Presets = new System.Windows.Forms.CheckBox[16];


        static private Sim staticSimpleTutorial = null;		//This is for the static printLine() method. It's static so other classes can easily call it.

        #region GUI components
        private System.ComponentModel.Container components = null;
        private System.Windows.Forms.Panel viewport_panel;
        private System.Windows.Forms.CheckBox gravity_checkBox;
        private System.Windows.Forms.Button resetScene_button;
        #endregion

        #region Renderer Stuff (Non physics related)
        Device renderDevice = null;
        Matrix cameraMatrix;
        bool[] keyStates = new bool[256];
        float nearClipDistance = 0.01f;
        float farClipDistance = 2000.0f;
        float verticalFOV = 60.0f;
        bool pauseFlag = false;
        bool startedFlag = false;
        #endregion

        NxPhysicsSDK physicsSDK = null;
        NxScene physicsScene = null;
        MyDebugRenderer physicsDebugRenderer = null;						//This is built into the wrapper to provide a D3D debug renderer for the physics

        Vector3 physicsGravity = new Vector3(0, -9.8f, 0);						//The units are arbitrary. Let's just say this is -9.8 m/s, which means 1 physics unit equals 1 meter.
        float visualizeScale = 1; //0.5f;											//Actor axes, joint axes, normals.... are scaled by this value

        //There's an array which is used to smooth out the framerate for the physics timestep
        float lastTime = 0;
        float lastTimeStep = 0;
        int timeStepIndex = 0;
        float[] timeStepArray = null;

        Vector3 cameraRot, cameraPos;

        private MenuStrip menuStrip1;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem loadToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem simulationToolStripMenuItem;
        private ToolStripMenuItem startToolStripMenuItem;
        private ToolStripMenuItem stopToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private Label label5;
        private Label currentServo;
        private CheckBox resetclearflg;
        private Label label4;
        private Label label1;
        private Label accelInfo;
        private CheckBox drenderStatus;
        private Button RunButton;
        private Button stepButton;
        private Label label3;
        private ToolStripMenuItem loadRBMToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private ToolStripMenuItem loadRBAToolStripMenuItem;
        private CheckBox hookflg;
        private Label label2;

        public Sim()
        {
            int i;

            InitializeComponent();
            Directory.SetCurrentDirectory(Application.StartupPath);

            timeStepArray = new float[32];
            for (i = 0; i < timeStepArray.Length; i++)	//Initialize the array to a reasonable target frame rate of 60 FPS.
            { timeStepArray[i] = 1 / 60.0f; }

            // --------------------------------------------------------------------------- 

            for (i = 0; i < 5; i++)
            {
                System.Windows.Forms.CheckBox c = new System.Windows.Forms.CheckBox();
                // 
                // checkBox1
                // 
                c.AutoSize = true;
                c.Location = new System.Drawing.Point(351 + i * 80, 555);
                c.Name = "checkBox" + (i + 1);
                c.Size = new System.Drawing.Size(80, 17);
                c.Text = "checkBox" + (i + 1);
                c.UseVisualStyleBackColor = true;
                c.Visible = false;
                c.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
                this.Controls.Add(c);
                Presets[i] = c;
            }

            for (i = 0; i < 16; i++)
            {
                System.Windows.Forms.Label s = new System.Windows.Forms.Label();
                System.Windows.Forms.HScrollBar v = new System.Windows.Forms.HScrollBar();

                // 
                // label1
                // 
                s.AutoSize = true;
                s.Location = new System.Drawing.Point(24, 120 + i * 22);
                s.Name = "label" + i;
                s.Size = new System.Drawing.Size(35, 13);
                s.Text = "ID" + i;
                s.MouseClick += new MouseEventHandler(this.servoSelect);

                // 
                // servoValue
                // 
                v.Location = new System.Drawing.Point(120, 120 + i * 22);
                v.Name = "S" + i;
                v.Size = new System.Drawing.Size(70, 20);
                v.Minimum = 0;
                v.Maximum = 269;
                v.Value = 135;
                v.ValueChanged += new System.EventHandler(this.servoVal_Changed);

                this.Controls.Add(s);
                this.Controls.Add(v);

                servoTag[i] = s;
                servoValue[i] = v;
                setCS(0);

                Presets[0].Text = "Basic";
                Presets[0].Visible = true;
                Presets[1].Text = "Zero";
                Presets[1].Visible = true;
                nextfree = 2;

            }

            //servoValue[0].KeyDown += new System.EventHandler(this.listBox_KeyDown);
            //servoValue[0].KeyPress += new System.EventHandler(this.listBox_KeyPress);

            reset = false;
            setPressed = false;
            runPressed = false;
            stepPressed = false;
            bodyvis = true;   // 
            setDR(false);
            rbc = new RoboController();
            sga = new simpleGA();

            // ---------------------------------------
            setMsg("RoboSimulator 0.1 started");
            servos = new Servo[MaxServos];
            setServos(servos);

            // ---------------------------------------
            cameraRot = new Vector3(4, 8, -14); //(- 20, 0, 0);
            cameraPos = new Vector3(2, 10, -23); //(0, 5, -10);
        }


        //This is outside the constructor because physicsDebugRenderer relies upon the
        // renderDevice to be initialized before it is created in startPhysics()
        public void appStarted()
        {
            staticSimpleTutorial = this;	//This is for the static printLine() method
            setProperFocus();			//set focus to get key events using a wacky technique

            if (startPhysics())
            {
                resetScene();
                //printInfo();
            }
        }

        //////////////////////////////PHYSICS CODE BELOW////////////////////////////////
        public bool startPhysics()
        {
            //If you don't have your own NxUserOutputStream just use NxPhysicsSDK.Create() instead
            physicsSDK = NxPhysicsSDK.Create();
            if (physicsSDK == null)
            {
                MessageBox.Show("Failed to start physics", "Error", MessageBoxButtons.OK);
                this.Close();
                return false;
            }

            //There are several createScene() methods. In this case the simplest is used.
            physicsScene = physicsSDK.createScene(physicsGravity);
            if (physicsScene == null)
            {
                MessageBox.Show("Failed to create scene", "Error", MessageBoxButtons.OK);
                this.Close();
                return false;
            }

            scene = physicsScene;

            //Pass in the D3D renderDevice to the NovodexDebugRenderer. You can make your own DebugRenderer by inheriting NxUserDebugRenderer and implementing the renderData() method
            physicsDebugRenderer = new MyDebugRenderer(renderDevice);
            physicsDebugRenderer.DrawLineShadows = true;

            //These need to be set for the debugRenderer to have anything to render
            physicsSDK.setParameter(NxParameter.NX_VISUALIZATION_SCALE, visualizeScale);	//Things like actor axes and joints will be drawn to the size of visualizeScale
            physicsSDK.setParameter(NxParameter.NX_VISUALIZE_ACTOR_AXES, 1);				//Set to non-zero to visualize
            physicsSDK.setParameter(NxParameter.NX_VISUALIZE_JOINT_LIMITS, 1);
            physicsSDK.setParameter(NxParameter.NX_VISUALIZE_BODY_JOINT_LIST, 1);
            physicsSDK.setParameter(NxParameter.NX_VISUALIZE_COLLISION_SHAPES, 1);

            //Set the skin width to 0.01 meters
            physicsSDK.setParameter(NxParameter.NX_SKIN_WIDTH, 0.01f);
            physicsSDK.setParameter(NxParameter.NX_VISUALIZE_ACTIVE_VERTICES, 1);

            return true;
        }


        public void killPhysics()
        {
            if (physicsSDK != null)
            {
                if (physicsScene != null)
                { physicsSDK.releaseScene(physicsScene); }
                physicsSDK.release();
                physicsSDK = null;
                physicsScene = null;
            }
        }


        //This uses an array to cache and average timeSteps to maintain a smooth simulation.
        public float getTimeStep()
        {
            float time = NovodexUtil.getTimeInSeconds();
            float deltaTime = time - lastTime;
            deltaTime = NovodexUtil.clampFloat(deltaTime, 1 / 200.0f, 1 / 30.0f);	//Clamp deltaTime between 200 FPS and 30 FPS. This disallow freakish numbers to effect the smoothing. (Like the first deltaTime and any large time caused by pausing the physics)
            lastTime = time;

            timeStepArray[timeStepIndex] = deltaTime;
            timeStepIndex = (timeStepIndex + 1) % timeStepArray.Length;

            float sum = 0;
            for (int i = 0; i < timeStepArray.Length; i++)
            { sum += timeStepArray[i]; }

            float timeStep = sum / timeStepArray.Length;	//Return the average of timeStepArray
            lastTimeStep = timeStep;
            return timeStep;
        }

        public float getLastTimeStep()
        { return lastTimeStep; }



        public void tickPhysics()
        {
            if (!pauseFlag)
            {
                physicsScene.simulate(getTimeStep());	//Run the physics for X seconds
                physicsScene.flushStream();				//Flush any commands that haven't been run yet
                physicsScene.fetchResults(NxSimulationStatus.NX_RIGID_BODY_FINISHED, true);	//Get the results of the simulation which is required before the next call to simulate()

                if (Form.MouseButtons == MouseButtons.Right)
                {
                    Point mousePos = viewport_panel.PointToClient(Form.MousePosition);
                    //NxRay ray=NovodexUtil.getRayFromLeftHandedPerspectiveViewport(mousePos.X,mousePos.Y,viewport_panel.ClientSize.Width,viewport_panel.ClientSize.Height,verticalFOV,cameraMatrix);
                    //pokeObject(ray);
                }

                float dx, dy, dz;
                if (com != null && kit.getBodyActor() != null)
                {
                    dx = kit.getBodyActor().CMassGlobalPosition.X - com.X;
                    dz = kit.getBodyActor().CMassGlobalPosition.Y - com.Y;
                    dy = kit.getBodyActor().CMassGlobalPosition.Z - com.Z;
                
                    showAccel(dx, dy, dz);
                    com = kit.getBodyActor().CMassGlobalPosition;
                }

                if (simulation)
                {
                    simTrigger = false;
                    Console.WriteLine("Debug: " + kit.getBodyActor().CMassGlobalPosition.Y);
                    TimeSpan t = DateTime.Now.Subtract(rt);

                    // max time per test
                    if (Convert.ToInt32(t.TotalMilliseconds) > 12000)
                    {
                        Console.WriteLine("Debug: More than 10s");
                        simUpdate(Convert.ToInt32(t.TotalMilliseconds) * (int)kit.getBodyActor().CMassGlobalPosition.Y);
                    }

                    if (kit.getBodyActor().CMassGlobalPosition.Y < 3)
                    {
                        simUpdate(Convert.ToInt32(t.TotalMilliseconds));
                        Console.WriteLine("Debug: Trigger at:" + Convert.ToInt32(t.TotalMilliseconds));
                        simTrigger = true;
                    }

                }

            }
        }

        // ----------------------------------------------------------------------------------------

        private void render()
        {
            if (renderDevice == null)
            { return; }

            renderDevice.Clear(ClearFlags.ZBuffer | ClearFlags.Target, System.Drawing.Color.CornflowerBlue, 1.0f, 0);
            renderDevice.BeginScene();
            drawScene();
            renderDevice.EndScene();
            renderDevice.Present();
        }

        private void drawScene()
        {
            setupView();
            if (debugRender)
            {
                physicsDebugRenderer.renderData(physicsScene.getDebugRenderable());
            }
            else
            {
                RenderActors();
                RenderJoints();
            }
        }

        void RenderJoints()
        {
            // Render all the actors in the scene 
            foreach (NxJoint j in scene.getJoints())
            {
                Color c = Color.Blue;
                if (j.getJointType() == NxJointType.NX_JOINT_FIXED) c = Color.Blue;
                if (j.getJointType() == NxJointType.NX_JOINT_REVOLUTE) c = Color.Red;

                NxActor a;
                NxActor b;
                j.getActors(out a, out b);

                physicsDebugRenderer.drawline(a.getGlobalPosition(), b.getGlobalPosition(), c);
            }
        }

        void RenderActors()
        {
            // Render all the actors in the scene 
            int nbActors = scene.getNbActors();
            //Console.WriteLine("Actors = " + nbActors);

            NxActor[] actors = scene.getActors();
            foreach (NxActor actor in actors)
            {
                //if (actor.UserData != null) Console.WriteLine("Userdata=" + actor.UserData);

                foreach (NxShape s in actor.getShapes())
                {
                    if (s.getShapeType() == NxShapeType.NX_SHAPE_BOX)
                    {
                        drawbox(s, actor.UserData.ToInt32());
                    }
                    if (s.getShapeType() == NxShapeType.NX_SHAPE_PLANE)
                    {
                        physicsDebugRenderer.drawplane();
                    }
                    if (s.getShapeType() == NxShapeType.NX_SHAPE_SPHERE)
                    {
                    }
                    if (s.getShapeType() == NxShapeType.NX_SHAPE_CAPSULE)
                    {
                        drawbox(s, actor.UserData.ToInt32());
                    }
                }
            }
        }

        void drawbox(NxShape s, int n)
        {
            if (renderDevice == null)
            { return; }

            renderDevice.RenderState.ZBufferEnable = true;   // We'll not use this feature
            renderDevice.RenderState.Lighting = true;        // Or this one...
            renderDevice.RenderState.CullMode = Cull.None;    // Or this one...

            Mesh m;
            Matrix wp;
            bool sel = false;

            if (n > 99)
            {
                if (n - 100 == current_servo)
                {
                    //Console.WriteLine("Sel=" + (n - 100));
                    sel = true;
                }
                n = 1;
            }

            if (bodyvis == false && n == 5) return;

            if (models[n].modelsmesh != null)
            {
                m = models[n].modelsmesh;
                wp = Matrix.Scaling(models[n].scale) * models[n].pose;

                renderDevice.Transform.World = wp * s.getGlobalPose();

                for (int i = 0; i < models[n].mat.Length; ++i)
                {
                    if (models[n].txtr[i] != null)
                    {
                        renderDevice.SetTexture(0, models[n].txtr[i]);
                    }

                    Material t = models[n].mat[i];

                    if (sel)
                    {
                        t.Diffuse = Color.Yellow;
                    }

                    renderDevice.Material = t;
                    m.DrawSubset(i);
                }
            }
            else
            {
                NxBoxShapeDesc b = (NxBoxShapeDesc)s.getShapeDesc();

                m = cubeMesh;
                wp = Matrix.Scaling(new Vector3(2 * b.dimensions.X, 2 * b.dimensions.Y, 2 * b.dimensions.Z));
                renderDevice.Material = servoMat;
                renderDevice.Transform.World = wp * s.getGlobalPose();
                m.DrawSubset(0);
            }

            // ... and use the world matrix to spin and translate the teapot  
            // out where we can see it...
        }

        // ----------------------------------------------------------------------------------------

        //This will delete all the actors in the scene and then rebuild the start scene and reset the camera
        public void resetScene()
        {
            //Get a list of all the triangleMeshes, convexMeshes, and heightFields associated with this scene so I can release them
            ArrayList meshList = NovodexUtil.getAllMeshesAssociatedWithScene(physicsScene, true, true, true);

            ControllerManager.purgeControllers();	//Put this above releaseAllActorsFromScene() because it deletes the actors associated with the controllers. If it is put below Novodex will complain about releasing the actors in the controllers twice. It won't crash, it just complains.
            NovodexUtil.releaseAllActorsFromScene(physicsScene); //When an actor is released any joint attached to it will be released. Releasing all actors will release all joints.
            NovodexUtil.releaseAllClothsFromScene(physicsScene);
            NovodexUtil.releaseAllMaterialsFromScene(physicsScene);

            //Just to be safe wait until after the actors were released before releasing the triangleMeshes, convexMeshes, and heightFields.
            NovodexUtil.releaseMeshes(physicsSDK, meshList);
            NovodexUtil.seedRandom(9812219);

            createPhysicsStuff();

            rt = DateTime.Now;

            tickPhysics(); //Call tick once so the renderer looks correct even if the physics is paused
        }



        //This manually wakes up every object in the scene. When you turn gravity back on it is possible for motionless actors to remain floating in the air. That's why when you hit the gravity checkBox it will call this
        public void wakeUpScene()
        {
            NxActor[] actorArray = physicsScene.getActors();
            foreach (NxActor actor in actorArray)
            { actor.wakeUp(); }
        }



        public void createPhysicsStuff()
        {
            //Make it so the default material isn't frictionless
            physicsScene.setDefaultMaterial(new NxMaterialDesc(0.5f, 0.6f, 0.0f));

            //Create static ground plane using descriptors
            NxPlaneShapeDesc planeDesc = NxPlaneShapeDesc.Default;	//The default plane is the ground plane so no parameters need to be changed
            NxBodyDesc bodyDesc = null;								//using a null bodyDesc makes the actor static
            NxActorDesc planeActorDesc = new NxActorDesc(planeDesc, bodyDesc, 1, Matrix.Identity);
            NxActor planeActor = physicsScene.createActor(planeActorDesc);
            planeActor.Name = "Ground_Plane";

            initSetup(true);
        }

        #region Direct3D and Windows Code
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.viewport_panel = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.currentServo = new System.Windows.Forms.Label();
            this.gravity_checkBox = new System.Windows.Forms.CheckBox();
            this.resetScene_button = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.loadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadRBMToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadRBAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.simulationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.startToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resetclearflg = new System.Windows.Forms.CheckBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.accelInfo = new System.Windows.Forms.Label();
            this.drenderStatus = new System.Windows.Forms.CheckBox();
            this.RunButton = new System.Windows.Forms.Button();
            this.stepButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.hookflg = new System.Windows.Forms.CheckBox();
            this.viewport_panel.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // viewport_panel
            // 
            this.viewport_panel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(255)))), ((int)(((byte)(128)))));
            this.viewport_panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.viewport_panel.Controls.Add(this.label5);
            this.viewport_panel.Controls.Add(this.currentServo);
            this.viewport_panel.Location = new System.Drawing.Point(272, 27);
            this.viewport_panel.Name = "viewport_panel";
            this.viewport_panel.Size = new System.Drawing.Size(512, 524);
            this.viewport_panel.TabIndex = 0;
            //this.viewport_panel.Paint += new System.Windows.Forms.PaintEventHandler(this.viewport_panel_Paint);
            this.viewport_panel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.viewport_panel_MouseDown);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Arial", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(294, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(61, 22);
            this.label5.TabIndex = 66;
            this.label5.Text = "label5";
            this.label5.Visible = false;
            // 
            // currentServo
            // 
            this.currentServo.AutoSize = true;
            this.currentServo.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.currentServo.Location = new System.Drawing.Point(3, 496);
            this.currentServo.Name = "currentServo";
            this.currentServo.Size = new System.Drawing.Size(25, 25);
            this.currentServo.TabIndex = 65;
            this.currentServo.Text = "0";
            // 
            // gravity_checkBox
            // 
            this.gravity_checkBox.Checked = true;
            this.gravity_checkBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.gravity_checkBox.Location = new System.Drawing.Point(7, 529);
            this.gravity_checkBox.Name = "gravity_checkBox";
            this.gravity_checkBox.Size = new System.Drawing.Size(64, 24);
            this.gravity_checkBox.TabIndex = 0;
            this.gravity_checkBox.TabStop = false;
            this.gravity_checkBox.Text = "Gravity";
            this.gravity_checkBox.CheckedChanged += new System.EventHandler(this.gravity_checkBox_CheckedChanged);
            // 
            // resetScene_button
            // 
            this.resetScene_button.Location = new System.Drawing.Point(71, 528);
            this.resetScene_button.Name = "resetScene_button";
            this.resetScene_button.Size = new System.Drawing.Size(59, 23);
            this.resetScene_button.TabIndex = 0;
            this.resetScene_button.TabStop = false;
            this.resetScene_button.Text = "Reset";
            this.resetScene_button.Click += new System.EventHandler(this.resetScene_button_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem1,
            this.simulationToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(803, 24);
            this.menuStrip1.TabIndex = 51;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loadToolStripMenuItem,
            this.loadRBMToolStripMenuItem,
            this.loadRBAToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(37, 20);
            this.toolStripMenuItem1.Text = "File";
            // 
            // loadToolStripMenuItem
            // 
            this.loadToolStripMenuItem.Name = "loadToolStripMenuItem";
            this.loadToolStripMenuItem.Size = new System.Drawing.Size(139, 22);
            this.loadToolStripMenuItem.Text = "Load Config";
            this.loadToolStripMenuItem.Click += new System.EventHandler(this.loadToolStripMenuItem_Click);
            // 
            // loadRBMToolStripMenuItem
            // 
            this.loadRBMToolStripMenuItem.Name = "loadRBMToolStripMenuItem";
            this.loadRBMToolStripMenuItem.Size = new System.Drawing.Size(139, 22);
            this.loadRBMToolStripMenuItem.Text = "Load RBM";
            this.loadRBMToolStripMenuItem.Click += new System.EventHandler(this.loadRBMToolStripMenuItem_Click);
            // 
            // loadRBAToolStripMenuItem
            // 
            this.loadRBAToolStripMenuItem.Name = "loadRBAToolStripMenuItem";
            this.loadRBAToolStripMenuItem.Size = new System.Drawing.Size(139, 22);
            this.loadRBAToolStripMenuItem.Text = "Load RBA";
            this.loadRBAToolStripMenuItem.Click += new System.EventHandler(this.loadRBAToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(139, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // simulationToolStripMenuItem
            // 
            this.simulationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startToolStripMenuItem,
            this.stopToolStripMenuItem});
            this.simulationToolStripMenuItem.Name = "simulationToolStripMenuItem";
            this.simulationToolStripMenuItem.Size = new System.Drawing.Size(76, 20);
            this.simulationToolStripMenuItem.Text = "Simulation";
            // 
            // startToolStripMenuItem
            // 
            this.startToolStripMenuItem.Name = "startToolStripMenuItem";
            this.startToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.startToolStripMenuItem.Text = "Start";
            this.startToolStripMenuItem.Click += new System.EventHandler(this.startToolStripMenuItem_Click);
            // 
            // stopToolStripMenuItem
            // 
            this.stopToolStripMenuItem.Name = "stopToolStripMenuItem";
            this.stopToolStripMenuItem.Size = new System.Drawing.Size(98, 22);
            this.stopToolStripMenuItem.Text = "Stop";
            this.stopToolStripMenuItem.Click += new System.EventHandler(this.stopToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // resetclearflg
            // 
            this.resetclearflg.AutoSize = true;
            this.resetclearflg.Location = new System.Drawing.Point(136, 533);
            this.resetclearflg.Name = "resetclearflg";
            this.resetclearflg.Size = new System.Drawing.Size(90, 17);
            this.resetclearflg.TabIndex = 58;
            this.resetclearflg.Text = "keep values?";
            this.resetclearflg.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label4.Location = new System.Drawing.Point(7, 607);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 15);
            this.label4.TabIndex = 56;
            this.label4.Text = "Message: ";
            // 
            // label1
            // 
            this.label1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label1.Location = new System.Drawing.Point(71, 607);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(713, 19);
            this.label1.TabIndex = 54;
            this.label1.Text = "label1";
            // 
            // accelInfo
            // 
            this.accelInfo.AutoSize = true;
            this.accelInfo.Location = new System.Drawing.Point(98, 27);
            this.accelInfo.Name = "accelInfo";
            this.accelInfo.Size = new System.Drawing.Size(75, 13);
            this.accelInfo.TabIndex = 64;
            this.accelInfo.Text = "Accelerometer";
            // 
            // drenderStatus
            // 
            this.drenderStatus.AutoSize = true;
            this.drenderStatus.Checked = true;
            this.drenderStatus.CheckState = System.Windows.Forms.CheckState.Checked;
            this.drenderStatus.Location = new System.Drawing.Point(25, 64);
            this.drenderStatus.Name = "drenderStatus";
            this.drenderStatus.Size = new System.Drawing.Size(78, 17);
            this.drenderStatus.TabIndex = 63;
            this.drenderStatus.Text = "Render Off";
            this.drenderStatus.UseVisualStyleBackColor = true;
            this.drenderStatus.CheckedChanged += new System.EventHandler(this.drenderStatus_CheckedChanged);
            // 
            // RunButton
            // 
            this.RunButton.Location = new System.Drawing.Point(201, 60);
            this.RunButton.Name = "RunButton";
            this.RunButton.Size = new System.Drawing.Size(43, 24);
            this.RunButton.TabIndex = 62;
            this.RunButton.Text = "Run";
            this.RunButton.UseVisualStyleBackColor = true;
            this.RunButton.Click += new System.EventHandler(this.RunButton_Click);
            // 
            // stepButton
            // 
            this.stepButton.Location = new System.Drawing.Point(150, 60);
            this.stepButton.Name = "stepButton";
            this.stepButton.Size = new System.Drawing.Size(45, 23);
            this.stepButton.TabIndex = 61;
            this.stepButton.Text = "Step";
            this.stepButton.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(118, 92);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 13);
            this.label3.TabIndex = 60;
            this.label3.Text = "Servo Value";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(32, 92);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(49, 13);
            this.label2.TabIndex = 59;
            this.label2.Text = "Servo ID";
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // hookflg
            // 
            this.hookflg.AutoSize = true;
            this.hookflg.Location = new System.Drawing.Point(7, 560);
            this.hookflg.Name = "hookflg";
            this.hookflg.Size = new System.Drawing.Size(52, 17);
            this.hookflg.TabIndex = 65;
            this.hookflg.Text = "Hook";
            this.hookflg.UseVisualStyleBackColor = true;
            this.hookflg.CheckedChanged += new System.EventHandler(this.hookflg_CheckedChanged);
            // 
            // Sim
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(803, 626);
            this.Controls.Add(this.hookflg);
            this.Controls.Add(this.accelInfo);
            this.Controls.Add(this.drenderStatus);
            this.Controls.Add(this.RunButton);
            this.Controls.Add(this.stepButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.resetclearflg);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.gravity_checkBox);
            this.Controls.Add(this.resetScene_button);
            this.Controls.Add(this.viewport_panel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Sim";
            this.Text = "RoboSimulator 0.1N";
            this.viewport_panel.ResumeLayout(false);
            this.viewport_panel.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        [STAThread]
        static void Main()
        {
            using (Sim simpleTutorial = new Sim())
            {
                if (!simpleTutorial.InitializeGraphics())
                {
                    MessageBox.Show("Could not initialize Direct3D");
                    return;
                }
                simpleTutorial.Show();
                simpleTutorial.appStarted();

                while (simpleTutorial.Created)	//Main loop
                {
                    simpleTutorial.processKeys();

                    if (simpleTutorial.startedFlag)
                    {
                        simpleTutorial.tickPhysics();
                    }
                    simpleTutorial.render();

                    simpleTutorial.updateMotion();

                    //Sim.purgePrintList();
                    Application.DoEvents();
                }
                simpleTutorial.killPhysics();	//be nice and properly release the physics
            }
        }

        // --------------------------------------------------------------------------------------------------



        // --------------------------------------------------------------------------------------------------

        public bool InitializeGraphics()
        {
            try
            {
                PresentParameters presentParams = new PresentParameters();
                presentParams.Windowed = true;
                presentParams.SwapEffect = SwapEffect.Copy; //.Discard;

                presentParams.AutoDepthStencilFormat = DepthFormat.D16;
                presentParams.EnableAutoDepthStencil = true;

                renderDevice = new Device(0, DeviceType.Hardware, this.viewport_panel, CreateFlags.MixedVertexProcessing, presentParams);
                init();
                return true;
            }
            catch (DirectXException e)
            {
                MessageBox.Show(e.Message, "InitializeGraphics Error");
                return false;
            }
        }

        private Mesh cubeMesh;
        private Material servoMat;

        public struct ModelData
        {
            public int object_type; //0=servo
            public string name;     //servo
            public Matrix pose;
            public Vector3 scale;
            public Mesh modelsmesh;
            public Material[] mat;
            public Texture[] txtr;
        };

        ModelData[] models;
        int MaxModels = 10;

        void loadmodels()
        {
            models = new ModelData[MaxModels];

            for (int id = 0; id < MaxModels; id++)
            {
                string mb;
                if ((mb = IniData.getParameter("M" + id)) != "")
                {
                    /*
                    #Model, scale vec, rot vec, translate
                    M1=servo,servo6,0.55, 0.55, 0.55, 0, 0, 0
                    */
                    string[] n = mb.Split(',');

                    models[id].name = n[0];
                    models[id].pose = Matrix.RotationYawPitchRoll(
                        float.Parse(n[5].Trim()) * NovodexUtil.DEG_TO_RAD,
                        -float.Parse(n[6].Trim()) * NovodexUtil.DEG_TO_RAD,
                        float.Parse(n[7].Trim()) * NovodexUtil.DEG_TO_RAD);
                    models[id].scale = new Vector3(
                        float.Parse(n[2].Trim()),
                        float.Parse(n[3].Trim()),
                        float.Parse(n[4].Trim()));

                    if (n.Length > 8)
                    {
                        models[id].pose *= Matrix.Translation(
                            float.Parse(n[8].Trim()),
                            float.Parse(n[9].Trim()),
                            float.Parse(n[10].Trim()));
                    }

                    ExtendedMaterial[] exMaterials;
                    models[id].modelsmesh = Mesh.FromFile(n[1] + ".x", MeshFlags.Managed, renderDevice, out exMaterials);

                    if (models[id].txtr != null)
                    {
                        //DisposeTextures();
                    }

                    models[id].txtr = new Texture[exMaterials.Length];
                    models[id].mat = new Material[exMaterials.Length];

                    for (int i = 0; i < exMaterials.Length; ++i)
                    {
                        if (exMaterials[i].TextureFilename != null)
                        {
                            string texturePath =
                             Path.Combine(Path.GetDirectoryName(n[1]), exMaterials[i].TextureFilename);
                            models[id].txtr[i] = TextureLoader.FromFile(renderDevice, texturePath);
                        }
                        models[id].mat[i] = exMaterials[i].Material3D;
                        models[id].mat[i].Ambient = models[id].mat[i].Diffuse;
                    }
                }
            }
        }

        void init()
        {
            renderDevice.Lights[0].Type = LightType.Directional;
            renderDevice.Lights[0].Direction = new Vector3(0.5f, -0.8f, 0.7f);
            renderDevice.Lights[0].Diffuse = Color.White;
            renderDevice.Lights[0].Specular = Color.White;
            renderDevice.Lights[0].Enabled = true;

            // Setup a material for the teapot
            servoMat = new Material();
            servoMat.Diffuse = Color.DarkOrange;
            servoMat.Emissive = Color.DarkSalmon;
            cubeMesh = Mesh.FromFile("cube3.x", MeshFlags.Managed, renderDevice);
        }



        public void setupView()
        {
            float aspectRatio = ((float)viewport_panel.ClientSize.Width) / ((float)viewport_panel.ClientSize.Height);
            cameraMatrix = Matrix.Translation(cameraPos) * Matrix.RotationYawPitchRoll(cameraRot.Y * NovodexUtil.DEG_TO_RAD, -cameraRot.X * NovodexUtil.DEG_TO_RAD, 0);
            NovodexUtil.setMatrixPos(ref cameraMatrix, cameraPos);

            Vector3 dir = NovodexUtil.getMatrixZaxis(ref cameraMatrix);
            Vector3 target = cameraPos + dir;
            Vector3 upDir = new Vector3(0, 1, 0);

            renderDevice.Transform.View = Matrix.LookAtLH(cameraPos, target, upDir);
            renderDevice.Transform.Projection = Matrix.PerspectiveFovLH(verticalFOV * NovodexUtil.DEG_TO_RAD, aspectRatio, nearClipDistance, farClipDistance);
        }

        private void clearKeyStates()
        {
            for (int i = 0; i < 256; i++)
            { keyStates[i] = false; }
        }

        private void processKeys()
        {
            if (this != Form.ActiveForm)	//Lost focus so reset the keys
            { clearKeyStates(); }

            if (keyStates[(int)Keys.NumPad1])
            {
                int p = servos[current_servo].getPos();
                setServoValue(current_servo, p + 1);
                keyStates[(int)Keys.NumPad1] = false;
            }


            if (keyStates[(int)Keys.V])
            {
                bodyvis = !bodyvis;
                keyStates[(int)Keys.V] = false;
            }


            if (keyStates[(int)Keys.NumPad3])
            {
                int p = servos[current_servo].getPos();
                setServoValue(current_servo, p - 1);
                keyStates[(int)Keys.NumPad3] = false;
            }
            if (keyStates[(int)Keys.Insert])
            {
                // next servo in use (i.e. non-null)
                do { current_servo = (current_servo + 1) % 16; } while (servos[current_servo] == null);
                selectedActor = servos[current_servo].getActor();
                setCS(current_servo);
                keyStates[(int)Keys.Insert] = false;
            }
            if (keyStates[(int)Keys.Delete])
            {
                do
                {
                    current_servo = (current_servo - 1) % 16;
                    if (current_servo < 0) current_servo = 15;
                } while (servos[current_servo] == null);
                selectedActor = servos[current_servo].getActor();
                setCS(current_servo);

                keyStates[(int)Keys.Delete] = false;
            }

            driveObject(ref cameraRot, ref cameraPos, false);
        }

        void driveObject(ref Vector3 objectRot, ref Vector3 objectPos, bool alternateKeysFlag)
        {
            float driveScale = 0.2f;
            float rotateScale = 2.0f;
            Vector3 driveInput = new Vector3(0, 0, 0);
            Vector3 rotateInput = new Vector3(0, 0, 0);

            if (keyStates[(int)Keys.ShiftKey])
            {
                driveScale *= 3.0f;
                rotateScale *= 2.0f;
            }

            if (!alternateKeysFlag)
            {
                if (keyStates[(int)Keys.W])
                { driveInput.Z += 1; }
                if (keyStates[(int)Keys.S])
                { driveInput.Z -= 1; }
                if (keyStates[(int)Keys.A])
                { driveInput.X -= 1; }
                if (keyStates[(int)Keys.D])
                { driveInput.X += 1; }
                if (keyStates[(int)Keys.R])
                { driveInput.Y += 1; }
                if (keyStates[(int)Keys.F])
                { driveInput.Y -= 1; }
                if (keyStates[(int)Keys.Q] || keyStates[(int)Keys.Left])
                { rotateInput.Y -= 1; }
                if (keyStates[(int)Keys.E] || keyStates[(int)Keys.Right])
                { rotateInput.Y += 1; }
                if (keyStates[(int)Keys.Up])
                { rotateInput.X += 1; }
                if (keyStates[(int)Keys.Down])
                { rotateInput.X -= 1; }
            }
            else
            {
                if (keyStates[(int)Keys.I])
                { driveInput.Z += 1; }
                if (keyStates[(int)Keys.K])
                { driveInput.Z -= 1; }
                if (keyStates[(int)Keys.J])
                { driveInput.X -= 1; }
                if (keyStates[(int)Keys.L])
                { driveInput.X += 1; }
                if (keyStates[(int)Keys.Y])
                { driveInput.Y += 1; }
                if (keyStates[(int)Keys.H])
                { driveInput.Y -= 1; }
                if (keyStates[(int)Keys.U])
                { rotateInput.Y -= 1; }
                if (keyStates[(int)Keys.O])
                { rotateInput.Y += 1; }
            }

            driveInput *= driveScale;
            rotateInput *= rotateScale;

            objectRot.X = NovodexUtil.clampFloat(objectRot.X + rotateInput.X, -85, 85);	//clamp angle between -85 and 85 degrees
            objectRot.Y += rotateInput.Y;

            Vector3 dir = new Vector3((float)Math.Sin(NovodexUtil.DEG_TO_RAD * objectRot.Y), 0, (float)Math.Cos(NovodexUtil.DEG_TO_RAD * objectRot.Y));
            Vector3 perpDir = new Vector3(dir.Z, 0, -dir.X);
            Vector3 upDir = new Vector3(0, 1, 0);

            objectPos = objectPos + (dir * driveInput.Z) + (perpDir * driveInput.X) + (upDir * driveInput.Y);
        }

        private void gravity_checkBox_CheckedChanged(object sender, System.EventArgs e)
        {
            if (gravity_checkBox.Checked)
            {
                physicsScene.Gravity = physicsGravity;
                wakeUpScene();	//If something is floating motionless in air it is asleep and won't fall down. This makes sure to wake everything up when gravity is turned back on
            }
            else
            { physicsScene.Gravity = new Vector3(0, 0, 0); }
            setProperFocus();
        }

        private void resetScene_button_Click(object sender, System.EventArgs e)
        {
            //startedFlag=false;
            reset = true;
            setRunning(false);
            resetScene();
            setProperFocus();
        }

        private void garbageCollect_button_Click(object sender, System.EventArgs e)
        {
            GC.Collect();
            Process process = Process.GetCurrentProcess();

            int used = 0, unused = 0;
            NxInterfaceStats stats = (NxInterfaceStats)physicsSDK.getInterface(NxInterfaceType.NX_INTERFACE_STATS, 1);
            stats.getHeapSize(out used, out unused);

            printLine("");
            printLine(String.Format("Managed Memory: {0:F3} MB", ((float)GC.GetTotalMemory(false)) / 1048576));
            printLine(String.Format("Unmanaged Memory: {0:F3} MB", ((float)(process.WorkingSet - GC.GetTotalMemory(false))) / 1048576));
            printLine(String.Format("Total Memory: {0:F3} MB", ((float)process.WorkingSet) / 1048576));
            printLine("HeapSize: used=" + used + " unused=" + unused);

            setProperFocus();
        }


        private void viewport_panel_MouseDown(object sender, MouseEventArgs e)
        {
            if (startedFlag)
            {
                NxRay ray = NovodexUtil.getRayFromLeftHandedPerspectiveViewport(e.X, e.Y, viewport_panel.ClientSize.Width, viewport_panel.ClientSize.Height, verticalFOV, cameraMatrix);
                //raycastTest(ray);
            }
            startedFlag = true;
        }

        //Need to steal the key presses from the listBox so it doesn't act weird while driving around
        private void listBox_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        { e.Handled = true; startedFlag = true; }
        private void listBox_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        { e.Handled = true; }

        private void setProperFocus()	//Since the viewport panel doesn't get key events I just focus the listBox and steal it's key events.
        {
            //label1.Focus();
            servoValue[0].Focus();
        }


        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        protected override bool ProcessKeyPreview(ref Message m)	//This is required because the viewport panel receives no key events.
        {
            //if(startedFlag)
            {
                switch (m.Msg)
                {
                    case WM_KEYDOWN:
                        if ((((uint)m.LParam) >> 30 & 1) == 0)
                        { keyStates[(int)m.WParam] = true; }
                        break;
                    case WM_KEYUP:
                        keyStates[(int)m.WParam] = false;
                        break;
                }
            }
            return base.ProcessKeyPreview(ref m);
        }

        //printLine() doesn't print immediately because NxUserOutputStream and the listBox don't play nice together. If I try to immediately add the message to the listBox it crashes. Calling something like Debug.WriteLine() works though.
        static ArrayList printList = new ArrayList();
        static public void printLine(string message)
        { printList.Add(message); }
        static public void purgePrintList()
        {
        }
        #endregion

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 n = new AboutBox1();
            n.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit the simulator?", "Robosimulator 0.1", MessageBoxButtons.OKCancel) == DialogResult.OK)
                this.Dispose();
        }


        public void showAccel(float x, float y, float z)
        {
            if (runPressed || setPressed)
            {
                int a;
                string s = "Accel=(";
                a = (int)(x * 100);
                s += string.Format("{0:G}", a) + ",";
                a = (int)(y * 100);
                s += string.Format("{0:G}", a) + ",";
                a = (int)(z * 100);
                s += string.Format("{0:G}", a) + ")";

                if (accelInfo.Text != s) accelInfo.Text = s;
            }
            rbc.setAccel(x, y, z);
        }

        public void updateStatus(string s, string t)
        {
            if (!(resetclearflg.Checked && reset))
            {
                servoValue[Int32.Parse(s)].Value = Int32.Parse(t);
                setMsg("Motion running: " + (rbc.frame_number + 1) + "/" + (rbc.scene_number + 1));
            }
        }

        public void setMsg(string s)
        {
            label1.Text = s;
        }

        private void servoVal_Changed(object sender, EventArgs e)
        {
            //slider changed
            System.Windows.Forms.HScrollBar v = (System.Windows.Forms.HScrollBar)sender;
            int n = Int32.Parse(v.Name.Substring(1));
            setCS(n);
            current_servo = n;
            setServoValue(n, v.Value);
            setProperFocus();
        }

        private void servoSelect(object sender, EventArgs e)
        {
            //slider changed
            System.Windows.Forms.Label l = (System.Windows.Forms.Label)sender;
            string s = l.Text.Substring(2, 1);
            if (l.Text.Length > 3 && Char.IsNumber(l.Text[3]))
                s += l.Text.Substring(3, 1);

            int n = Int32.Parse(s);
            setCS(n);
            setProperFocus();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //Preset motions
            System.Windows.Forms.CheckBox c = (System.Windows.Forms.CheckBox)sender;
            playMotion(c.Text, 10);
            c.Checked = false;
            setRunning(true);
        }

        public void setCS(int n)
        {
            int old = Int32.Parse(currentServo.Text);
            currentServo.Text = n.ToString();
            servoTag[old].BackColor = Color.White;
            servoTag[n].BackColor = Color.Yellow;
            current_servo = n;
        }

        public void setServoValue(int n, int v)
        {
            servoTag[n].Text = "ID" + n + "=" + v.ToString();
            rbc.setPos(n, v);
        }

        public void setServos(Servo[] s)
        {
            rbc.setServo(s);
        }

        public void setPresets(string psv)
        {
            string ns = "Prog" + (nextfree + 1);

            int pi = psv.IndexOf(":");

            if (pi > 0)
            {
                ns = psv.Substring(0, pi);
                psv = psv.Substring(pi + 1);
            }
            if (rbc.setMotions(ns + ":" + psv) == 0)
            {
                Presets[nextfree].Text = ns;
                Presets[nextfree].Visible = true;
                if (nextfree < 4) nextfree++;
            }
        }

        public void setRunning(bool f)
        {
            runPressed = f;
            startedFlag = f;

            if (runPressed)
                RunButton.Text = "Stop";
            else
                RunButton.Text = "Run";
        }

        public void setDR(bool f)
        {
            debugRender = f;
            drenderStatus.Checked = f;
        }


        // process Ini file
        void initSetup(bool full)
        {
            IniData = new IniManager();
            if (filename == null || filename == "")
                filename = "one.txt";

            IniData.Load(filename); // use default

            loadmodels();

            string mb;
            kit = new Parts(scene, joints, actors);


            if ((mb = IniData.getParameter("CAMERA")) != "")
            {
                string[] t = mb.Split(',');

                cameraRot = new Vector3(float.Parse(t[4]), float.Parse(t[5]), float.Parse(t[6]));
                cameraPos = new Vector3(float.Parse(t[0]), float.Parse(t[1]), float.Parse(t[2]));
            }


            for (int id = 0; id < 6; id++)
            {
                if ((mb = IniData.getParameter("MOTION"+id)) != "")
                    setPresets(mb);
            }

            if ((mb = IniData.getParameter("RENDER")) != "")
                setDR(mb.ToUpper().StartsWith("T") || mb.ToUpper().StartsWith("Y"));


            for (int id = 0; id < MaxServos; id++)
            {
                if (IniData.getParameter("S" + id) != "")
                    initScene("S" + id, IniData.getParameterEvalArray("S" + id), full);
            }

            for (int id = 0; id < MaxJoints; id++)
            {
                if (IniData.getParameter("Joint" + id) != "")
                    initScene("J" + id, IniData.getParameterEvalArray("Joint" + id), full);
            }

            if (IniData.getParameter("FootL") != "")
                initScene("K0", IniData.getParameterEvalArray("FootL"), full);
            if (IniData.getParameter("FootR") != "")
                initScene("K0", IniData.getParameterEvalArray("FootR"), full);
            if (IniData.getParameter("HandR") != "")
                initScene("K1", IniData.getParameterEvalArray("HandR"), full);
            if (IniData.getParameter("HandL") != "")
                initScene("K1", IniData.getParameterEvalArray("HandL"), full);
            if (IniData.getParameter("Body") != "")
                initScene("K2", IniData.getParameterEvalArray("Body"), full);
            if (IniData.getParameter("KneeL") != "")
                initScene("K3", IniData.getParameterEvalArray("KneeL"), full);
            if (IniData.getParameter("KneeR") != "")
                initScene("K3", IniData.getParameterEvalArray("KneeR"), full);

            if ((mb = IniData.getParameter("Sel")) != "")
                initScene("SEL:" + mb, null, full);
            else
                initScene("SEL:0", null, full);

            if ((mb = IniData.getParameter("RUN")) != "")
            {
                setRunning(mb.ToUpper().StartsWith("T") || mb.ToUpper().StartsWith("Y"));
            }

            if ((mb = IniData.getParameter("Basic")) != "")
            {
                playMotion(mb, 1);
            }

            hookflg.Checked = false;
            if ((mb = IniData.getParameter("Hook")) != "")
            {
                if (mb.ToUpper().StartsWith("T") || mb.ToUpper().StartsWith("Y"))
                {
                    hookflg.Checked = true;
                }
            }
        
        }

        public void initScene(string sn, float[] x, bool full)
        {
            if (sn.StartsWith("SEL:"))
            {
                int id = Int32.Parse(sn.Substring(4));

                current_servo = id;
                selectedActor = servos[id].getActor();
                updateStatus(sn.Substring(4), servos[current_servo].getPos().ToString());
                return;
            }

            if (sn.StartsWith("S"))
            {
                int id = Int32.Parse(sn.Substring(1));

                if (full)
                    servos[id] = new Servo(sn.Substring(1), new Vector3(x[0], x[1], x[2] + zoff),
                            -x[3], x[4], x[5], scene, actors, joints);

                updateStatus(sn.Substring(1), servos[id].getPos().ToString());
            }
            if (sn.StartsWith("J") && full)
            {
                int id = Int32.Parse(sn.Substring(1));

                kit.createJoint(servos[Convert.ToInt32(x[0])], servos[Convert.ToInt32(x[1])], Convert.ToInt32(x[2]));
            }
            if (sn.StartsWith("K") && full)
            {
                int id = Int32.Parse(sn.Substring(1));
                switch (id)
                {
                    case 0:
                        kit.CreateFoot(new Vector3(x[0], x[1], x[2] + zoff), servos[Convert.ToInt32(x[3])]);
                        break;
                    case 1:
                        kit.CreateHand(new Vector3(x[0], x[1], x[2] + zoff), servos[Convert.ToInt32(x[3])], Convert.ToInt32(x[4]));
                        break;
                    case 2:
                        kit.CreateBody(new Vector3(x[0], x[1], x[2] + zoff),
                            servos[Convert.ToInt32(x[3])], servos[Convert.ToInt32(x[4])],
                            servos[Convert.ToInt32(x[5])], servos[Convert.ToInt32(x[6])]);
                        break;
                    case 3:
                        kit.CreateKnee(new Vector3(x[0], x[1], x[2] + zoff), servos[Convert.ToInt32(x[3])].getActor(), servos[Convert.ToInt32(x[4])].getActor());
                        break;
                }
            }

        }

        //--------------------------------------------------------------------------------------------

        public void playMotion(string a, int b)
        {
            if (reset == true && resetclearflg.Checked == true)
            {
                for (int i = 0; i < 16; i++)
                    rbc.setPos(i, servoValue[i].Value);
                return;
            }
            rbc.playMotion(a, b, 250);
        }

        public void updateMotion()
        {
            if (rbc.updateMotion())
            {
                for (int i = 0; i < 16; i++)
                    servoValue[i].Value = rbc.getPos(i);
                setMsg("Motion running: " + (rbc.frame_number + 1) + "/" + (rbc.scene_number + 1));
            }
        }

        //--------------------------------------------------------------------------------------------
        // GA simulation

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // simulation start
            simulation = true;

            label4.Text = "SIMULATION";     // show in GA sim mode
            string r = sga.run(50);          // just 3 generations
            rbc.setMotions("SIM:" + r);
            rbc.playMotion("SIM", 1, 250);
            label5.Text = "P=" + sga.pool + " G=" + sga.generation;
            label5.Visible = true;

            resetclearflg.Checked = true;   // keep values between reset ?
            setRunning(true);               // start runnung if no already
            setHook(false);
            reset = true;
        }

        public void simUpdate(int n)
        {
            if (simulation == false)
            {
                return;
            }
            setMsg("Simulation trigger = " + n);

            reset = true;
            resetScene();
            setHook(false);

            string r = sga.update(n);
            label5.Text = "P=" + sga.pool + " G=" + sga.generation + " BF=" + sga.bestthispool;
            if (r != "")
            {
                rbc.setMotions("SIM:" + r);
                rbc.playMotion("SIM", 1, 250);
            }
            else
            {
                simulation = false;
                label4.Text = "Message:";
                setMsg("Sim completed - best=" + sga.bestever + "(last gen=" + sga.bestthispool + ")");
                setRunning(false);               // done
            }
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            simulation = false;
            label4.Text = "Message:";
            label5.Visible = false;
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            setRunning(!runPressed);
            setProperFocus();
        }

        private void drenderStatus_CheckedChanged(object sender, EventArgs e)
        {
            debugRender = drenderStatus.Checked;
            setProperFocus();
        }

        private void loadRBMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filename = "";

            // load rbm file
            openFileDialog1.Filter = "Motion File|*.rbm";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = openFileDialog1.FileName;
            }
            setMsg("RBM: " + filename);

            try
            {
                TextReader tr = new StreamReader(filename);
                string line = "";
                string mb = "";
                int p;
                while ((line = tr.ReadLine()) != null)
                {
                    line = line.Trim();
                    Console.WriteLine(line);
                    string[] a = line.Split(':');
                    for (int i = 0; i < a.Length; i++)
                    {
                        Console.WriteLine(i + "," + a[i]);
                    }
                    mb = a[6] + ":";

                    int c = 13; // start of 00 postion

                    for (int i = 0; i < 15; i++)
                    {
                        mb += a[c + 5 + i * 6] + ",";
                    }

                    c = 111; // start of motion

                    while (c + 101 <= a.Length + 1)
                    {
                        for (int i = 0; i < 15; i++)
                        {
                            mb += a[c + 5 + i * 6] + ",";
                        }
                        c += 101;
                    }
                }
                tr.Close();
                Console.WriteLine(mb);
                setPresets(mb);
            }
            catch (Exception e1)
            {
                Console.WriteLine("RBM load Exception - " + e1.ToString());
            }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Config File|*.txt";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = openFileDialog1.FileName;
            }
            setMsg("Load file - " + filename);
            resetScene();
        }

        private void loadRBAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filename = "";

            // load rbm file
            openFileDialog1.Filter = "Action File|*.rba";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = openFileDialog1.FileName;
            }
            setMsg("RBA: " + filename);

            try
            {
                TextReader tr = new StreamReader(filename);
                string line = "";
                while ((line = tr.ReadLine()) != null)
                {
                    line = line.Trim();
                    Console.WriteLine(line);
                }
                tr.Close();
                Console.WriteLine(line);
                rbc.setAction(line);

            }
            catch (Exception e1)
            {
                Console.WriteLine("RBA load Exception - " + e1.ToString());
            }
        }

        public void setHook(bool f)
        {

            NxActor t = kit.getBodyActor();

            if (t == null) { t = servos[0].getActor(); }
            if (t != null)
            {
                if (f)
                {
                    t.raiseBodyFlag(NxBodyFlag.NX_BF_KINEMATIC);
                }
                else
                {
                    t.clearBodyFlag(NxBodyFlag.NX_BF_KINEMATIC);
                }
            }
        }

        private void hookflg_CheckedChanged(object sender, EventArgs e)
        {
            setHook(hookflg.Checked);
        }

    }
}



