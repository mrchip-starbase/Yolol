﻿p="100010101100"
:o=p---p>:done++goto2
i=:i:o=i%2*i%3*i%5*i%7*i%11*(i!=169)>:done++goto3

/--------//--------//--------//--------//--------//--------//--------/

p="10100010101100"
:o=p---p>:done++goto2
i=:i:o=i%2*i%3*i%5*i%7*i%11*i%13>:done++goto3

/--------//--------//--------//--------//--------//--------//--------/

v="0000"w="1000"x=w+10z=x-10+"00"y="0"+z+z+10p="010"+x+z+v+"1"+y+"0"+y
p+=z+v+"10"+z+w+z+v+v+x+x+w+z+"00"+z+w+z+"10"+w+z+"1"+y+"0010"+w+z
p+="10"+z+x+x+x+"101100"
:o=p---p>0:done=1goto4

/--------//--------//--------//--------//--------//--------//--------/

x=100010z=x-10y="0"+z+z+10p="010"+x+z+"00001"+y+"0"+y+z+"000010"+z
p+="1000"+z+"00000000"+x+x+"1000"+z+"00"+z+"1000"+z+"101000"+z+"1"+y
p+="00101000"+z+"10"+z+x+x+x+"101100"
:o=p---p>0:done=1goto4

/--------//--------//--------//--------//--------//--------//--------/

p="010100010100000000010100000100000100010000010000010100000000010100"
p+="00010001000000000000010001010001010001000000010000010001000001010"
p+="00100000101000001000001000101000100000101000001000101000101000101"
p+="01100"
:o=p---p>0:done=1goto5

/--------//--------//--------//--------//--------//--------//--------/

:o=0:done=1p="199,197,193,191,181,179,173,167,163,157,151,149,139,137"
:o=0:done=1p+=",131,127,113,109,107,103,101,97,89,83,79,73,71,67,61,5"
:o=1:done=1p+="9,53,47,43,41,37,31,29,23,19,17,13,11,7,5"b=10
:o=1:done=1s="98743"
:o=0:done=1n=0j=0  
c=p---p x/=(c>".")d=5*(c>4)+2*(s>s-c)d+=c>d d+=c>d n+=d*b^j++goto6
:done=1 :o=n==:i goto7-:o*2

/--------//--------//--------//--------//--------//--------//--------/

2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59,
61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131,
137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199