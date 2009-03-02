using System;
using System.Collections.Generic;
using System.Text;

namespace Simulator
{
    class simpleGA
    {
        //geneotype
        private int NoGenes = 16;
        //mutation rate, change it have a play
        private double MutationRate = 0.1;
        //recomination rate
        private double RecombinationRate = 0.5;

        int[,] gene;
        int[] fitness;

        int seed=1234;
        Random rn;
        public int generation;
        int genEnd;
        public int pool;
        int poolMax = 10;
        public int bestthispool;
        public int bestever;

        public  simpleGA()
        {
            rn = new Random(seed);
            gene = new int[poolMax,NoGenes];         // genome
            fitness = new int[poolMax];
        }

        public void spawn()
        {
            for (int j = 0; j < poolMax; j++)
            {
                for (int i = 0; i < NoGenes; i++)
                {
                    gene[j,i] = rn.Next(270);
                }
            }
        }


        string convert(int n)
        {
            int i;
            string s = "";
            for (i = 0; i < NoGenes-1; i++)
            {
                s += gene[n,i].ToString() + ",";
            }
            s += gene[n,i].ToString();
            return s;
        }

        public string run(int max_gen)
        {
            bestthispool = 0;
            bestever = 0;
            generation = 0;
            pool = 0;
            spawn();
            genEnd = max_gen;
            return convert(pool);
        }
        
        public string update(int n)
        {
            if (pool < poolMax-1)
            {
                fitness[pool] = n;
                pool++;
                return convert(pool);
            }

            if (generation <= genEnd)
            {

                // identify string / fitest in pool

                int c = 0; 
                int f = 0;
                for (int i = 0; i < poolMax; i++)
                {
                    if (fitness[i] > f)
                    {
                        c = i;
                        f = fitness[i];
                    }
                }
                Console.WriteLine("Generation: " + generation + " Fitest: " + c + "Value = " + f);
                bestthispool = f;
                if (bestthispool > bestever) bestever = bestthispool;

                // breed next generation from fitest
                breed(c);

                // start again

                generation++;
                pool = 0;
                return convert(pool);
            }
            else
            {
                return ""; // finshed
            }
        }



        void breed(int c)
        {
            //breed 'poolMax' of new siblings
            //c was winner

            for (int j = 0; j < poolMax; j++)
            {
                if (j != c) // leave fitest alone
                {
                    for (int i = 0; i < NoGenes; i++)
                    {
                        if (rn.NextDouble() < RecombinationRate)
                        {
                            gene[j,i] = gene[c,i];        //straight clone
                        }
                        else if (rn.NextDouble() < MutationRate)
                        {
                            gene[j,i] = rn.Next(269);      //mutation
                        }
                        else
                            gene[j,i] = gene[c,i] + j;     //clone variation
                    }
                }
            }
        }
    }
}
