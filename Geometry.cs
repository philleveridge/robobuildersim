//By Jason Zelsnack

using System;
using System.IO;
using System.Collections;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using NovodexWrapper;




namespace Simulator
{
	public class LineData
	{
		public string str=null;
		public int offset=0;
		
		public void setString(string str)
		{
			this.str=str;
			offset=0;
		}

		public bool beginsWith(string prefix)
			{return beginsWith(prefix,false);}

		public bool beginsWith(string prefix,bool caseSensitive)
		{
			for(int i=offset;i<str.Length;i++)
			{
				if(!Char.IsWhiteSpace(str[i]))
				{
					if(i+prefix.Length>=str.Length)
						{return false;}
					
					for(int j=0;j<prefix.Length;j++)
					{
						char ch0=str[i+j];
						char ch1=prefix[j];
						if(!caseSensitive)
						{
							ch0=char.ToLower(ch0);
							ch1=char.ToLower(ch1);
						}
						
						if(ch0!=ch1)
							{return false;}
					}
					return true;
				}
			}
			return false;
		}

		//I wrote my own parser because int.parse() was being a jerk
		private int parseInt(int index)
		{
			offset=index;
			
			int sign=1;
			if(str[offset]=='-')
			{
				sign=-1;
				offset++;
			}

			int num=0;			
			for(;offset<str.Length;offset++)
			{
				char ch=str[offset];
				if(ch>='0' && ch<='9')
					{num=(num*10)+((int)(ch-'0'));}
				else
					{break;}
			}

			return num*sign;
		}

		//I wrote my own parser because float.parse() was being a jerk
		private float parseFloat(int index)
		{
			offset=index;
			
			float sign=1;
			if(str[offset]=='-')
			{
				sign=-1;
				offset++;
			}

			float num=0;
			float fraction=0;
			for(;offset<str.Length;offset++)
			{
				char ch=str[offset];
				if(ch>='0' && ch<='9')
					{num=(num*10)+((int)(ch-'0'));}
				else
					{break;}
			}
			
			if(str[offset]=='.')
			{
				offset++;
				float fractionScalar=0.1f;
				for(;offset<str.Length;offset++)
				{
					char ch=str[offset];
					if(ch>='0' && ch<='9')
					{
						fraction+=((int)(ch-'0'))*fractionScalar;
						fractionScalar/=10;
					}
					else
						{break;}
				}
			}
			
			return (num+fraction)*sign;
		}

		public int readInt()
		{
			for(int i=offset;i<str.Length;i++)
			{
				char ch=str[i];	
				if(Char.IsNumber(ch) || ch=='-')
					{return parseInt(i);}
			}
			return 0;
		}

		public float readFloat()
		{
			for(int i=offset;i<str.Length;i++)
			{
				char ch=str[i];	
				if(Char.IsNumber(ch) || ch=='-')
					{return parseFloat(i);}
			}
			return 0;
		}
		
		public Vector3 readVector3()
			{return new Vector3(readFloat(),readFloat(),readFloat());}
	}




	public class Geometry
	{
		public Vector3[] vertexArray=null;
		public int[] triangleIndiceArray=null;
		public bool initedFlag=false;

		public Geometry()
			{}

		public Geometry(string fileName)
			{initedFlag=loadFromFile(fileName);}
			
		
		public int NumVerts
		{
			get
			{
				if(vertexArray==null)
					{return 0;}
				return vertexArray.Length;
			}
		}

		public int NumTris
		{
			get
			{
				if(triangleIndiceArray==null)
					{return 0;}
				return triangleIndiceArray.Length/3;
			}
		}

		public bool loadFromFile(string fileName)
		{
			try
			{
				LineData lineData=new LineData();
				Stream inputStream=new FileStream(fileName,FileMode.Open,FileAccess.Read);
				StreamReader streamReader=new StreamReader(inputStream);

				int numVerts=0;
				int numTris=0;
				int vertIndex=0;
				int triIndex=0;
				
				while(streamReader.Peek() != -1)
				{
					lineData.setString(streamReader.ReadLine());					
					if(lineData.beginsWith("NumVertices"))
					{
						numVerts=lineData.readInt();
						vertexArray=new Vector3[numVerts];
					}
					else if(lineData.beginsWith("NumTris"))
					{
						numTris=lineData.readInt();
						triangleIndiceArray=new int[numTris*3];
					}
					else if(lineData.beginsWith("V:"))
					{
						vertexArray[vertIndex]=lineData.readVector3();
						vertIndex++;
					}
					else if(lineData.beginsWith("TI:"))
					{
						triangleIndiceArray[(triIndex*3)+0]=lineData.readInt();
						triangleIndiceArray[(triIndex*3)+1]=lineData.readInt();
						triangleIndiceArray[(triIndex*3)+2]=lineData.readInt();												
						triIndex++;
					}
				}
				
			}
			catch
			{
				Sim.printLine("Trouble parsing "+fileName);
				return false;
			}
			
			return true;
		}


	}
}

