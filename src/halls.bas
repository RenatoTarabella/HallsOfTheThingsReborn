REM >Halls
REM Halls of the Things - Crystal Computing / Design Design, 1983
REM by Simon Brattel, Martin Horsley and Neil Mottershead.
REM Rewritten in BBC BASIC V + ARM assembler from their Z80 source
REM (published by Simon Brattel), for personal use. The notes on the
REM original are in docs/halls-analisi.md; the Z80 labels are quoted.
REM A%-D% pass the arguments to the assembler, H% its workspace.
ON ERROR PROCerror
PROCinit
REPEAT
  PROCfront
  PROCgame
  PROCresult
UNTIL FALSE

REM ================= set up =================
DEF PROCinit
LOCAL i%,k%,r%,g%,b%,v%
MODE 9:OFF
*FX229,1
*FX4,1
REM Spectrum colours: 0-7 normal, 8-15 bright
FOR i%=0 TO 15
  v%=&D7:IF i%>7 THEN v%=&FF
  k%=i% AND 7:r%=0:g%=0:b%=0
  IF k% AND 2 THEN r%=v%
  IF k% AND 4 THEN g%=v%
  IF k% AND 1 THEN b%=v%
  VDU 19,i%,16,r%,g%,b%
NEXT
VDU 19,0,24,0,0,0
TR%=300:REM game cycles (GameLoop) per second
HS%=100:HN$="Kickaha"
DIM M% 16384+256,Z% 256,CH% 2048,G% 8192,FT% 8*4096,SV% 4*16384
DIM CT% 24,WDF% 64,IB% 32*32,ML% 32*64,PL% 32*128
DIM FSP%(7,32),FSC%(7,32),FSF%(7,32),FSD%(7),FTN%(7)
DIM RP%(8),RF%(8),SB%(6),LM%(4),T0%(7),T1%(7),T2%(7),WD%(7,5)
DIM CD%(7),CD1%(7),SWO%(3,4),SWC%(3,4),KN%(15),KP%(15),KW%(15)
REM ConTab: extra loops, doors, gaps for each floor
FOR i%=0 TO 7:READ T0%(i%):NEXT
FOR i%=0 TO 7:READ T1%(i%):NEXT
FOR i%=0 TO 7:READ T2%(i%):NEXT
DATA 0,255,200,150,127,100,60,20
DATA 0,150,100,40,23,13,7,4
DATA 0,200,100,50,30,20,10,5
REM WallDefs: 0-3 vertical walls, 4-7 horizontal
FOR i%=0 TO 7:FOR k%=0 TO 5:READ WD%(i%,k%):NEXT:NEXT
DATA &9C,&9C,&9C,&9C,&9C,&9C, &98,&DD,&DD,&DD,&DD,&94
DATA &98,&20,&20,&20,&20,&94, &20,&20,&20,&20,&20,&20
DATA &93,&93,&93,&93,&93,&93, &92,&DC,&DC,&DC,&DC,&91
DATA &92,&20,&20,&20,&20,&91, &20,&20,&20,&20,&20,&20
REM CDirect / CDirect1: up, right, down, left, then the diagonals
FOR i%=0 TO 7:READ CD%(i%):NEXT
FOR i%=0 TO 7:READ CD1%(i%):NEXT
DATA -128,1,128,-1,0,0,0,0
DATA -128,1,128,-1,-129,-127,129,127
REM the sword: offset and character of each phase, per direction
FOR i%=0 TO 3:FOR k%=0 TO 4:READ SWO%(i%,k%),SWC%(i%,k%):NEXT:NEXT
DATA 0,&E5, -1,&E6, 1,&E5, 1,&E4, -1,&E5
DATA 0,&E4, 128,&E7, 0,&E6, 0,&E7, -128,&E4
DATA 0,&E5, -1,&E4, 1,&E5, 1,&E6, -1,&E5
DATA 0,&E6, 128,&E7, 0,&E4, 0,&E7, -128,&E6
REM keys read on the edge: open, close, keep, drop, sword, arrow, fireball,
REM lightning, heal, status, centre, arrow left right up down, statistics
REM 1983: O C K D S A F L H 1 SPACE 5 8 7 6 W
REM modern: RETURN opens or closes, E Q CTRL SPACE F L H TAB C S (0 = none)
DIM KO%(15),KM%(15)
FOR i%=0 TO 15:READ KO%(i%):NEXT
FOR i%=0 TO 15:READ KM%(i%):NEXT
DATA -55,-83,-71,-51,-82,-66,-68,-87,-85,-49,-99,-20,-53,-37,-22,-34
DATA -74,0,-35,-17,-2,-99,-68,-87,-85,-97,-83,0,0,0,0,-82
STS%=TRUE:REM the statistics under the map
FOL%=TRUE:REM the screen follows the man (not in 1983: too slow on a Z80)
KS%=1:REM 1 = modern keys, 0 = those of 1983
REM a Thing: 64 bytes (the ML of the Z80), a projectile: 128 bytes (PL)
mPOS%=0:mCHR%=4:mTYPE%=8:mDEAD%=12:mMAG%=16:mMAX%=20:mTRE%=24:mCNT%=28
mREL%=32:mARR%=36:mHP%=40:mFLG%=44:mDIR%=48:mMARK%=52:mWC%=56:mWF%=60
pCNT%=0:pREL%=4:pPOS%=8:pDIAG%=12:pKIND%=16:pLIFE%=20:pCHR%=24:pBACK%=28
pSTEP1%=32:pERR%=36:pMIN%=40:pMAJ%=44:pMANF%=48:pTGT%=52:pTPOS%=56:pTGT2%=60
pBERR%=64:pBPOS%=68
PROCasm
REM the character set of 1983 (attributes + bitmaps), expanded for MODE 9
HD$=""
SYS "XOS_ReadVarVal","Halls$Dir",SV%,255,0,3 TO ,,i%;k%
IF (k% AND 1)=0 AND i%>0 THEN HD$="<Halls$Dir>."
REM Halls$Debug set: times on the bottom line, the eight floors saved as files
SYS "XOS_ReadVarVal","Halls$Debug",SV%,255,0,3 TO ,,i%;k%
DBG%=(k% AND 1)=0 AND i%>0
OSCLI "LOAD "+HD$+"Chars "+STR$~CH%
A%=CH%:B%=G%:CALL glyph%
PROCscreen
H%=ws%:REM the assembler finds its workspace in R7 (H%)
ws%!0=M%:ws%!16=G%:ws%!48=Z%:ws%!52=CT%:ws%!56=WDF%:ws%!60=cdtab:ws%!72=ML%:ws%!76=PL%
FOR i%=0 TO 7:CT%?i%=T0%(i%):CT%?(8+i%)=T1%(i%):CT%?(16+i%)=T2%(i%):NEXT
FOR i%=0 TO 7:FOR k%=0 TO 5:WDF%?(i%*8+k%)=WD%(i%,k%):NEXT:NEXT
ws%!44=RND:ws%!68=RND
ENDPROC

DEF PROCscreen
LOCAL b%
DIM b% 12
!b%=149:b%!4=-1
SYS "OS_ReadVduVariables",b%,b%
REM the 32x24 cells of the Spectrum in the middle of 40x32
ws%!12=!b%+32*160+16
ENDPROC

DEF PROCerror
*FX4,0
*FX229,0
ON
MODE 12
REPORT:PRINT " at line ";ERL
END

REM ================= assembler =================
DEF PROCasm
LOCAL pass%,code%
DIM code% 8192
FOR pass%=0 TO 2 STEP 2
P%=code%
[OPT pass%
.ws%
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD 0
\ ws 0 map, 4 frame x, 8 frame y, 12 screen, 16 glyphs
\ 20 door flag, 24 minor, 28 step1, 32 major, 36 diag, 40 err, 44 seed
\ 48 maze (16x16 rooms), 52 ConTab, 56 WallDefs, 60 directions, 64 MoveList
\ 68 random byte, 72 Things, 76 projectiles, 80 the man, 84 wounds,
\ 88 dead Things, 92 Virgin, 96 the man is dead
.wsa
EQUD ws%

\ draw a cell - R0 column, R1 row (of the 32x24), R2 code
.plotc
LDR R3,[R12,#12]
ADD R3,R3,R0,LSL #2
ADD R4,R1,R1,LSL #2
ADD R3,R3,R4,LSL #8
LDR R4,[R12,#16]
AND R2,R2,#255
ADD R4,R4,R2,LSL #5
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
LDR R5,[R4],#4
STR R5,[R3],#160
MOV PC,R14

.plot%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL plotc
LDMFD R13!,{R4-R12,PC}

\ OutIt with SCV - R0 map position, R1 code, R2 bit 0 = only on the screen
\ (uses R0-R5)
.put%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL putc
LDMFD R13!,{R4-R12,PC}
.putc
STMFD R13!,{R14}
MOV R0,R0,LSL #18
MOV R0,R0,LSR #18
TST R2,#1
LDREQ R3,[R12,#0]
STREQB R1,[R3,R0]
AND R2,R0,#127
LDR R3,[R12,#4]
SUB R2,R2,R3
AND R2,R2,#255
CMP R2,#32
BCS putx
MOV R3,R0,LSR #7
LDR R4,[R12,#8]
SUB R3,R3,R4
AND R3,R3,#255
CMP R3,#24
BCS putx
MOV R0,R2
MOV R2,R1
MOV R1,R3
BL plotc
.putx
LDMFD R13!,{PC}

\ NewScreen - the 32x24 cells from the map, X outside it
.redraw%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
MOV R7,#0
.rd1
MOV R6,#0
.rd2
LDR R8,[R12,#8]
ADD R8,R8,R7
AND R8,R8,#255
LDR R9,[R12,#4]
ADD R9,R9,R6
AND R9,R9,#255
MOV R2,#&FF
CMP R8,#113
BCS rd3
CMP R9,#128
BCS rd3
LDR R10,[R12,#0]
ADD R10,R10,R8,LSL #7
LDRB R2,[R10,R9]
.rd3
MOV R0,R6
MOV R1,R7
BL plotc
ADD R6,R6,#1
CMP R6,#32
BCC rd2
ADD R7,R7,#1
CMP R7,#24
BCC rd1
LDMFD R13!,{R4-R12,PC}

\ RangeDoor with InitLine and NextPoint - R0 from, R1 to, R2 range,
\ R3 not 0 = only set up the line. R0 out = 0 seen, 1 blocked
.los%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL losc
LDMFD R13!,{R4-R12,PC}
.losc
STMFD R13!,{R4-R11,R14}
AND R4,R0,#127
MOV R5,R0,LSR #7
AND R6,R1,#127
MOV R7,R1,LSR #7
SUBS R6,R6,R4
MOV R8,#0
MOVGT R8,#1
MVNLT R8,#0
RSBLT R6,R6,#0
SUBS R7,R7,R5
MOV R9,#0
MOVGT R9,#1
MVNLT R9,#0
RSBLT R7,R7,#0
CMP R3,#0
BNE lsetup
CMP R6,R2
BGE lfail
CMP R7,R2
BGE lfail
.lsetup
ADD R10,R8,R9,LSL #7
STR R10,[R12,#36]
CMP R7,R6
BLT lxmaj
MOV R11,R7
MOV R4,R6
MOV R5,R9,LSL #7
B lst
.lxmaj
MOV R11,R6
MOV R4,R7
MOV R5,R8
.lst
STR R11,[R12,#32]
STR R4,[R12,#24]
STR R5,[R12,#28]
MOV R6,R11,ASR #1
STR R6,[R12,#40]
MOV R7,#0
STR R7,[R12,#20]
CMP R3,#0
MOVNE R0,#0
BNE lexit
LDR R3,[R12,#0]
MOV R2,R0
MOV R1,R11
MOV R9,#1
.lloop
SUBS R1,R1,#1
BMI lpass
SUBS R6,R6,R4
ADDMI R2,R2,R10
ADDMI R6,R6,R11
ADDPL R2,R2,R5
MOV R2,R2,LSL #18
MOV R2,R2,LSR #18
LDRB R7,[R3,R2]
AND R8,R7,#&FE
CMP R8,#&DC
BEQ ldoor
AND R8,R7,#&F0
CMP R8,#&90
BEQ lfail
MOV R9,#0
B lloop
.ldoor
CMP R9,#0
BEQ lfail
MOV R8,#1
STR R8,[R12,#20]
MOV R9,#0
B lloop
.lpass
MOV R0,#0
B lexit
.lfail
MOV R0,#1
.lexit
LDMFD R13!,{R4-R11,PC}

\ Rand1 + Rand - the 32-bit generator of the Z80, R0 = low byte of the seed
\ (uses R0-R3)
.randc
LDR R0,[R12,#44]
MOV R1,R0,LSR #16
AND R2,R0,#&FF
MOV R2,R2,LSL #1
MOV R3,R1,LSR #14
AND R3,R3,#1
ORR R2,R2,R3
AND R2,R2,#&FF
MOV R2,R2,LSL #8
MOV R3,R1,LSR #6
AND R3,R3,#&FF
ORR R2,R2,R3
MOV R3,R0,LSL #16
MOV R3,R3,LSR #16
ADD R3,R3,R2
MOV R2,R0,LSR #7
MOV R2,R2,LSL #17
MOV R2,R2,LSR #17
ADD R1,R1,R2
ADD R1,R1,R3,LSR #16
MOV R3,R3,LSL #16
MOV R3,R3,LSR #16
TST R1,#&8000
MOV R1,R1,LSL #17
MOV R1,R1,LSR #17
ORR R0,R3,R1,LSL #16
ADDEQ R0,R0,#1
STR R0,[R12,#44]
AND R0,R0,#&FF
MOV PC,R14

.rand%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL randc
LDMFD R13!,{R4-R12,PC}

\ RandRange - R0 = Rand MOD R0 (0 when R0 is 0, without calling Rand)
\ (uses R0-R4)
.rrc
CMP R0,#0
MOVEQ PC,R14
MOV R4,R0
STMFD R13!,{R14}
BL randc
LDMFD R13!,{R14}
.rr1
CMP R0,R4
SUBCS R0,R0,R4
BCS rr1
MOV PC,R14

.rr%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL rrc
LDMFD R13!,{R4-R12,PC}

\ FindPosition - R0 = an empty cell in rows and columns 0-111 (uses R0-R3)
.fposc
STMFD R13!,{R14}
.fp1
BL randc
LDR R0,[R12,#44]
AND R1,R0,#&7F
CMP R1,#&70
BCS fp1
MOV R2,R0,LSR #8
AND R2,R2,#&3F
CMP R2,#&38
BCS fp1
AND R1,R0,#&FF
ORR R0,R1,R2,LSL #8
LDR R3,[R12,#0]
LDRB R1,[R3,R0]
CMP R1,#&20
BNE fp1
LDMFD R13!,{PC}

.fpos%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL fposc
LDMFD R13!,{R4-R12,PC}

\ Init - rows and columns 0-112 of the map become spaces (uses R0-R3)
.clrmc
LDR R0,[R12,#0]
MOV R1,#113
MOV R2,#&20
.cm1
MOV R3,#113
.cm2
STRB R2,[R0],#1
SUBS R3,R3,#1
BNE cm2
ADD R0,R0,#15
SUBS R1,R1,#1
BNE cm1
MOV PC,R14

.clrm%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL clrmc
LDMFD R13!,{R4-R12,PC}

\ R0 address, R1 bytes, R2 value
.fill%
SUBS R1,R1,#1
MOVMI PC,R14
STRB R2,[R0],#1
B fill%

\ MAIN / Main - hunt and kill on the 16x16 rooms, R0 = floor
\ R9 the current room, R10 ConTab + floor, R11 the rooms, R8 moves
.maze%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R11,[R12,#48]
LDR R10,[R12,#52]
ADD R10,R10,R0
MOV R1,#0
MOV R2,#0
.mz0
STRB R2,[R11,R1]
ADD R1,R1,#1
CMP R1,#256
BCC mz0
BL clrmc
BL randc
MOV R9,R0
.mzloop
BL findmoves
CMP R8,#0
BEQ mzd1
BL movec
B mzloop
.mzd1
BL randc
LDRB R1,[R10]
CMP R0,R1
BCS mznext
BL findlinks
CMP R8,#0
BEQ mznext
MOV R7,R9
BL movec
MOV R9,R7
.mznext
MOV R6,#256
.mzn1
ADD R9,R9,#1
AND R9,R9,#255
LDRB R0,[R11,R9]
CMP R0,#0
BEQ mzn2
SUBS R6,R6,#1
BNE mzn1
LDMFD R13!,{R4-R12,PC}
.mzn2
BL findlinks
CMP R8,#0
BEQ mznext
MOV R7,R9
BL movec
MOV R9,R7
B mzloop

\ FindMoves - unvisited neighbours (1 up, 2 down, 3 left, 4 right)
.findmoves
ADD R2,R12,#64
MOV R8,#0
TST R9,#&F0
BEQ fm1
SUB R0,R9,#16
LDRB R1,[R11,R0]
CMP R1,#0
MOVEQ R1,#1
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fm1
AND R0,R9,#&F0
CMP R0,#&F0
BEQ fm2
ADD R0,R9,#16
LDRB R1,[R11,R0]
CMP R1,#0
MOVEQ R1,#2
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fm2
TST R9,#15
BEQ fm3
SUB R0,R9,#1
LDRB R1,[R11,R0]
CMP R1,#0
MOVEQ R1,#3
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fm3
AND R0,R9,#15
CMP R0,#15
BEQ fm4
ADD R0,R9,#1
LDRB R1,[R11,R0]
CMP R1,#0
MOVEQ R1,#4
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fm4
MOV PC,R14

\ FindLinks - visited neighbours not joined to this room
.findlinks
ADD R2,R12,#64
MOV R8,#0
TST R9,#&F0
BEQ fk1
SUB R0,R9,#16
LDRB R1,[R11,R0]
CMP R1,#0
BEQ fk1
TST R1,#&30
MOVEQ R1,#1
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fk1
AND R0,R9,#&F0
CMP R0,#&F0
BEQ fk2
ADD R0,R9,#16
LDRB R1,[R11,R0]
CMP R1,#0
BEQ fk2
TST R1,#&C0
MOVEQ R1,#2
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fk2
TST R9,#15
BEQ fk3
SUB R0,R9,#1
LDRB R1,[R11,R0]
CMP R1,#0
BEQ fk3
TST R1,#3
MOVEQ R1,#3
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fk3
AND R0,R9,#15
CMP R0,#15
BEQ fk4
ADD R0,R9,#1
LDRB R1,[R11,R0]
CMP R1,#0
BEQ fk4
TST R1,#&C
MOVEQ R1,#4
STREQB R1,[R2,R8]
ADDEQ R8,R8,#1
.fk4
MOV PC,R14

\ Move - Random, then Rnd6 for the kind of passage, join R9 to the
\ chosen room and go there (uses R0-R5)
.movec
STMFD R13!,{R14}
CMP R8,#1
MOVEQ R4,#0
BEQ mv2
BL randc
AND R0,R0,#3
CMP R8,#2
ANDEQ R4,R0,#1
BEQ mv2
CMP R8,#4
MOVEQ R4,R0
BEQ mv2
.mv1
CMP R0,#2
MOVLS R4,R0
BLS mv2
BL randc
AND R0,R0,#3
B mv1
.mv2
BL randc
MOV R5,#1
LDRB R1,[R10,#8]
CMP R0,R1
BCC mv3
MOV R5,#2
LDRB R1,[R10,#16]
CMP R0,R1
MOVCS R5,#3
.mv3
ADD R2,R12,#64
LDRB R0,[R2,R4]
LDRB R1,[R11,R9]
CMP R0,#1
BEQ mvup
CMP R0,#2
BEQ mvdown
CMP R0,#3
BEQ mvleft
ORR R1,R1,R5
STRB R1,[R11,R9]
ADD R9,R9,#1
LDRB R1,[R11,R9]
ORR R1,R1,R5,LSL #2
STRB R1,[R11,R9]
LDMFD R13!,{PC}
.mvleft
ORR R1,R1,R5,LSL #2
STRB R1,[R11,R9]
SUB R9,R9,#1
LDRB R1,[R11,R9]
ORR R1,R1,R5
STRB R1,[R11,R9]
LDMFD R13!,{PC}
.mvdown
ORR R1,R1,R5,LSL #4
STRB R1,[R11,R9]
ADD R9,R9,#16
LDRB R1,[R11,R9]
ORR R1,R1,R5,LSL #6
STRB R1,[R11,R9]
LDMFD R13!,{PC}
.mvup
ORR R1,R1,R5,LSL #6
STRB R1,[R11,R9]
SUB R9,R9,#16
LDRB R1,[R11,R9]
ORR R1,R1,R5,LSL #4
STRB R1,[R11,R9]
LDMFD R13!,{PC}

\ Page / PAGE + PageC22 + SideWall - the rooms as characters, R0 = floor
.page%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R11,[R12,#48]
LDR R10,[R12,#0]
MOV R9,R0
CMP R9,#0
BNE pgrooms
BL clrmc
MOV R0,#&95
STRB R0,[R10]
MOV R1,#1
MOV R0,#&93
MOV R2,#&9C
.pg0
STRB R0,[R10,R1]
STRB R2,[R10,R1,LSL #7]
ADD R1,R1,#1
CMP R1,#112
BCC pg0
B pgc22
.pgrooms
MOV R8,#0
.pg1
AND R6,R8,#15
MOV R7,R8,LSR #4
RSB R0,R7,R7,LSL #3
RSB R1,R6,R6,LSL #3
ADD R5,R1,R0,LSL #7
LDRB R4,[R11,R8]
MOV R3,#&90
CMP R7,#0
BEQ pgc1
SUB R0,R8,#16
LDRB R0,[R11,R0]
AND R0,R0,#&C
CMP R0,#&C
ORRNE R3,R3,#8
.pgc1
AND R0,R4,#&C
CMP R0,#&C
ORRNE R3,R3,#4
CMP R6,#0
BEQ pgc2
SUB R0,R8,#1
LDRB R0,[R11,R0]
AND R0,R0,#&C0
CMP R0,#&C0
ORRNE R3,R3,#2
.pgc2
AND R0,R4,#&C0
CMP R0,#&C0
ORRNE R3,R3,#1
CMP R3,#&90
MOVEQ R3,#&20
STRB R3,[R10,R5]
LDR R2,[R12,#56]
MOV R0,R4,LSR #2
AND R0,R0,#3
ADD R0,R2,R0,LSL #3
ADD R1,R10,R5
MOV R3,#6
.pgw1
ADD R1,R1,#128
LDRB R2,[R0],#1
STRB R2,[R1]
SUBS R3,R3,#1
BNE pgw1
LDR R2,[R12,#56]
MOV R0,R4,LSR #6
AND R0,R0,#3
ADD R0,R0,#4
ADD R0,R2,R0,LSL #3
ADD R1,R10,R5
MOV R3,#6
.pgw2
ADD R1,R1,#1
LDRB R2,[R0],#1
STRB R2,[R1]
SUBS R3,R3,#1
BNE pgw2
ADD R8,R8,#1
CMP R8,#256
BCC pg1
.pgc22
ADD R1,R10,#&3800
MOV R0,#&99
STRB R0,[R1]
MOV R3,#1
.pgb1
SUB R2,R1,#128
LDRB R0,[R2,R3]
MOV R4,#&93
CMP R0,#&9C
CMPNE R0,#&94
MOVEQ R4,#&9B
STRB R4,[R1,R3]
ADD R3,R3,#1
CMP R3,#112
BCC pgb1
MOV R0,#&9A
STRB R0,[R1,#112]
MOV R0,#&96
STRB R0,[R10,#112]
MOV R3,#1
.pgs1
ADD R1,R10,R3,LSL #7
ADD R1,R1,#112
LDRB R0,[R1,#-1]
AND R0,R0,#&F0
CMP R0,#&90
MOVEQ R4,#&9E
MOVNE R4,#&9C
STRB R4,[R1]
ADD R3,R3,#1
CMP R3,#112
BCC pgs1
ADD R1,R10,#&3800
MOV R0,#&9A
STRB R0,[R1,#112]
RSB R0,R9,R9,LSL #3
RSB R0,R0,#104
ADD R1,R10,R0,LSL #7
ADD R1,R1,#112
MOV R0,#&94
STRB R0,[R1]
MOV R0,#&90
STRB R0,[R1,#-128]
STRB R0,[R1,#-256]
STRB R0,[R1,#-384]
STRB R0,[R1,#-512]
MOV R0,#&98
STRB R0,[R1,#-640]
LDMFD R13!,{R4-R12,PC}

\ InitMonsters - the 32 records from MonsterTypes (base + RandRange), in
\ the same order of calls as the Z80. R0 = 32 x 8 words - character,
\ kind of movement, magic, maximum magic, reload, arrows, hits, position
.imon%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
MOV R9,R0
MOV R8,#32
.im1
MOV R0,#8
BL rrc
ADD R0,R0,#&D0
STR R0,[R9],#4
MOV R0,#3
BL rrc
STR R0,[R9],#4
MOV R0,#20
BL rrc
MOV R0,#8
BL rrc
ADD R0,R0,#16
STR R0,[R9],#4
MOV R0,#16
BL rrc
ADD R0,R0,#32
STR R0,[R9],#4
MOV R0,#50
BL rrc
MOV R0,#90
BL rrc
ADD R0,R0,#60
STR R0,[R9],#4
MOV R0,#3
BL rrc
ADD R0,R0,#4
STR R0,[R9],#4
MOV R0,#10
BL rrc
MOV R0,#20
BL rrc
ADD R0,R0,#30
STR R0,[R9],#4
MOV R0,#20
BL rrc
BL fposc
STR R0,[R9],#4
SUBS R8,R8,#1
BNE im1
LDMFD R13!,{R4-R12,PC}

\ InitTreasure + InitMagic - 40 treasures and 40 potions
.itre%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R10,[R12,#0]
MOV R8,#40
.it1
BL fposc
MOV R7,R0
BL randc
AND R0,R0,#15
ORR R0,R0,#&B0
STRB R0,[R10,R7]
SUBS R8,R8,#1
BNE it1
MOV R8,#40
.it2
BL fposc
MOV R7,R0
BL randc
AND R0,R0,#15
ORR R0,R0,#&C0
STRB R0,[R10,R7]
SUBS R8,R8,#1
BNE it2
LDMFD R13!,{R4-R12,PC}

\ R1=0 - SaveFloor, treasures and potions of rows and columns 0-111
\ into R0 as (position << 8) + code, count in R0
\ R1=1 - CTres, sum of the treasures of the whole map
.scan%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R3,[R12,#0]
MOV R4,#0
CMP R1,#0
BNE sumall
MOV R5,#0
.sc1
MOV R6,#0
.sc2
ADD R7,R6,R5,LSL #7
LDRB R8,[R3,R7]
CMP R8,#&B0
BCC sc3
CMP R8,#&D0
BCS sc3
ORR R9,R8,R7,LSL #8
STR R9,[R0],#4
ADD R4,R4,#1
.sc3
ADD R6,R6,#1
CMP R6,#112
BCC sc2
ADD R5,R5,#1
CMP R5,#112
BCC sc1
MOV R0,R4
LDMFD R13!,{R4-R12,PC}
.sumall
MOV R5,#0
.sa1
LDRB R8,[R3,R5]
AND R9,R8,#&F0
CMP R9,#&B0
ANDEQ R9,R8,#15
ADDEQ R4,R4,R9
ADD R5,R5,#1
CMP R5,#16384
BCC sa1
MOV R0,R4
LDMFD R13!,{R4-R12,PC}

\ the 256 characters as MODE 9 pixels - R0 chars.bin, R1 table
\ 8 words per character, ink from the attribute (bright = +8)
.glyph%
STMFD R13!,{R4-R12,R14}
MOV R2,#0
.g1
LDRB R3,[R0,R2]
AND R4,R3,#7
TST R3,#64
ADDNE R4,R4,#8
TST R3,#7
MOVEQ R4,#0
MOV R5,#0
.g2
MOV R6,#0
CMP R2,#32
BCC g3
ADD R7,R5,R2,LSL #3
LDRB R6,[R0,R7]
.g3
MOV R8,#0
MOV R9,#0
.g4
MOV R11,R9,LSL #2
MOV R10,#128
TST R6,R10,LSR R9
ORRNE R8,R8,R4,LSL R11
ADD R9,R9,#1
CMP R9,#8
BCC g4
STR R8,[R1],#4
ADD R5,R5,#1
CMP R5,#8
BCC g2
ADD R2,R2,#1
CMP R2,#256
BCC g1
LDMFD R13!,{R4-R12,PC}

\ ---------------- the engine of the Things and of the projectiles ----------------
\ Records - Things 64 bytes at ws 72, projectiles 128 bytes at ws 76.
\ R12 ws, R11 map, R10 the record, R9 counter. Subroutines use R0-R5.

\ visible on the screen - R0 position, R1 out 1 or 0 (uses R1-R3)
.visc
AND R1,R0,#127
LDR R2,[R12,#4]
SUB R1,R1,R2
AND R1,R1,#255
CMP R1,#32
MOVCS R1,#0
MOVCS PC,R14
MOV R2,R0,LSR #7
LDR R3,[R12,#8]
SUB R2,R2,R3
AND R2,R2,#255
CMP R2,#24
MOVCS R1,#0
MOVCC R1,#1
MOV PC,R14

\ the refresh register of the Z80 - an independent random byte (uses R1)
.rnd8c
LDR R0,[R12,#68]
LDR R1,lcga
MUL R0,R1,R0
ADD R0,R0,#&3000
ADD R0,R0,#&39
STR R0,[R12,#68]
MOV R0,R0,LSR #16
AND R0,R0,#255
MOV PC,R14
.lcga
EQUD 1103515245

\ FPS - R0 = a free projectile or 0 (uses R1,R2)
.fpsc
LDR R0,[R12,#76]
MOV R1,#32
.fps1
LDR R2,[R0,#pLIFE%]
CMP R2,#0
MOVEQ PC,R14
ADD R0,R0,#128
SUBS R1,R1,#1
BNE fps1
MOV R0,#0
MOV PC,R14
.fps%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
BL fpsc
LDMFD R13!,{R4-R12,PC}

\ HitMan - R0 wounds
.hitmanc
LDR R1,[R12,#84]
ADD R1,R1,R0
STR R1,[R12,#84]
CMP R1,#127
MOVCS R1,#1
STRCS R1,[R12,#96]
MOV PC,R14

\ HitMonster - R0 position, R1 wounds, R2 corpse
.hitmonc
STMFD R13!,{R14}
LDR R3,[R12,#72]
MOV R4,#32
.hm1
LDR R5,[R3,#mPOS%]
CMP R5,R0
BEQ hm2
ADD R3,R3,#64
SUBS R4,R4,#1
BNE hm1
LDMFD R13!,{PC}
.hm2
LDR R5,[R3,#mHP%]
CMP R5,R1
SUBCS R5,R5,R1
STRCS R5,[R3,#mHP%]
LDMCSFD R13!,{PC}
MOV R5,#1
STR R5,[R3,#mDEAD%]
STR R2,[R3,#mCHR%]
LDR R5,[R12,#88]
CMP R5,#255
ADDCC R5,R5,#1
STR R5,[R12,#88]
MOV R1,R2
MOV R2,#0
BL putc
LDMFD R13!,{PC}
.hitmon%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R11,[R12,#0]
BL hitmonc
LDMFD R13!,{R4-R12,PC}

\ OpenIfNess - R0 position, R1 code
.openifc
CMP R1,#&DC
MOVEQ R2,#1
BEQ oi1
CMP R1,#&DD
MOVNE PC,R14
MOV R2,#128
.oi1
STMFD R13!,{R6-R8,R14}
MOV R6,R0
MOV R7,R2
.oi2
ADD R6,R6,R7
LDRB R1,[R11,R6]
AND R1,R1,#&FE
CMP R1,#&DC
BEQ oi2
MOV R8,#4
.oi3
SUB R6,R6,R7
MOV R0,R6
MOV R1,#&20
MOV R2,#0
BL putc
SUBS R8,R8,#1
BNE oi3
LDMFD R13!,{R6-R8,PC}

\ MoveT2Mark
.markc
LDR R0,[R10,#mPOS%]
STR R0,[R10,#mMARK%]
MOV R0,#1
STR R0,[R10,#mWC%]
MOV PC,R14

\ MonKeep - R0 position, R0 out 1 = taken
.monkeepc
STMFD R13!,{R14}
LDRB R1,[R11,R0]
AND R2,R1,#&F0
CMP R2,#&C0
BNE mk2
AND R1,R1,#15
LDR R2,[R10,#mMAG%]
ADD R2,R2,R1,LSL #1
CMP R2,#256
BCS mkno
LDR R3,[R10,#mMAX%]
CMP R2,R3
BCS mkno
STR R2,[R10,#mMAG%]
B mkyes
.mk2
CMP R2,#&B0
BNE mkno
LDR R2,[R10,#mTRE%]
CMP R2,#4
BCS mkno
ADD R2,R2,#1
STR R2,[R10,#mTRE%]
.mkyes
MOV R1,#&20
MOV R2,#0
BL putc
MOV R0,#1
LDMFD R13!,{PC}
.mkno
MOV R0,#0
LDMFD R13!,{PC}

\ MonODoor - R0 the cell to go to, R1 the step; R0 out the cell
.mondoorc
STMFD R13!,{R6,R7,R14}
MOV R0,R0,LSL #18
MOV R0,R0,LSR #18
MOV R6,R0
MOV R7,R1
LDRB R2,[R11,R0]
AND R3,R2,#&FE
CMP R3,#&DC
LDMNEFD R13!,{R6,R7,PC}
LDR R0,[R10,#mPOS%]
LDR R1,[R12,#80]
MOV R2,#50
MOV R3,#0
BL losc
CMP R0,#0
BNE md2
LDR R0,[R12,#20]
CMP R0,#0
BEQ md2
MOV R0,R6
LDRB R1,[R11,R6]
BL openifc
BL markc
SUB R0,R6,R7
LDMFD R13!,{R6,R7,PC}
.md2
BL rnd8c
TST R0,#3
MOVNE R0,R6
LDMNEFD R13!,{R6,R7,PC}
MOV R0,R6
LDRB R1,[R11,R6]
BL openifc
BL markc
MOV R0,R6
LDMFD R13!,{R6,R7,PC}

\ MTNewDir - R0 the cell
.newdirc
STMFD R13!,{R6,R14}
MOV R6,R0
BL monkeepc
CMP R0,#0
LDMNEFD R13!,{R6,PC}
BL rnd8c
AND R0,R0,#7
STR R0,[R10,#mDIR%]
LDRB R0,[R11,R6]
AND R0,R0,#&F0
CMP R0,#&90
LDMNEFD R13!,{R6,PC}
LDR R0,[R10,#mWF%]
CMP R0,#0
LDMEQFD R13!,{R6,PC}
BL rnd8c
AND R0,R0,#1
ADD R0,R0,#2
STR R0,[R10,#mTYPE%]
MOV R0,#0
STR R0,[R10,#mWF%]
BL markc
LDMFD R13!,{R6,PC}

\ WallFC - R0 the cell
.wallfcc
LDR R1,[R10,#mMARK%]
CMP R0,R1
MOVNE PC,R14
STMFD R13!,{R14}
BL markc
MOV R0,#1
STR R0,[R10,#mTYPE%]
STR R0,[R10,#mWF%]
BL rnd8c
AND R0,R0,#3
ORR R0,R0,#4
STR R0,[R10,#mDIR%]
LDMFD R13!,{PC}

\ the direction tables - R0 direction, R1 out the step
.cdc
LDR R1,[R12,#60]
AND R0,R0,#7
LDR R1,[R1,R0,LSL #2]
MOV PC,R14
.cd1c
LDR R1,[R12,#60]
AND R0,R0,#7
ADD R1,R1,#32
LDR R1,[R1,R0,LSL #2]
MOV PC,R14
.cdtab
EQUD -128
EQUD 1
EQUD 128
EQUD -1
EQUD 0
EQUD 0
EQUD 0
EQUD 0
EQUD -128
EQUD 1
EQUD 128
EQUD -1
EQUD -129
EQUD -127
EQUD 129
EQUD 127

\ MoveThing1 - wandering in 8 directions
.mt1c
STMFD R13!,{R6,R14}
LDR R0,[R10,#mDIR%]
BL cd1c
LDR R0,[R10,#mPOS%]
ADD R0,R0,R1
BL mondoorc
LDRB R1,[R11,R0]
CMP R1,#&20
BEQ mt1a
BL newdirc
LDMFD R13!,{R6,PC}
.mt1a
STR R0,[R10,#mPOS%]
MOV R6,R0
BL rnd8c
TST R0,#&70
MOVEQ R0,R6
BLEQ newdirc
LDMFD R13!,{R6,PC}

\ MoveThing2 (R8=1, right hand) and MoveThing3 (R8=-1, left hand)
.mt2c
STMFD R13!,{R6-R8,R14}
MOV R8,R0
LDR R0,[R10,#mWC%]
CMP R0,#0
BLEQ markc
LDR R0,[R10,#mWC%]
ADD R0,R0,#1
AND R0,R0,#255
STR R0,[R10,#mWC%]
LDR R6,[R10,#mDIR%]
MOV R0,R6
CMP R8,#1
ANDNE R0,R0,#3
BL cdc
LDR R0,[R10,#mPOS%]
ADD R0,R0,R1
BL mondoorc
LDRB R1,[R11,R0]
CMP R1,#&20
BNE mt2b
ADD R7,R6,R8
AND R7,R7,#3
MOV R0,R7
BL cdc
LDR R0,[R10,#mPOS%]
ADD R0,R0,R1
BL mondoorc
LDRB R1,[R11,R0]
CMP R1,#&20
STREQ R0,[R10,#mPOS%]
STREQ R7,[R10,#mDIR%]
BEQ mt2a
MOV R0,R6
BL cdc
LDR R0,[R10,#mPOS%]
ADD R0,R0,R1
MOV R0,R0,LSL #18
MOV R0,R0,LSR #18
STR R0,[R10,#mPOS%]
.mt2a
LDR R0,[R10,#mPOS%]
BL wallfcc
LDMFD R13!,{R6-R8,PC}
.mt2b
MOV R7,R0
BL monkeepc
CMP R0,#0
LDMNEFD R13!,{R6-R8,PC}
SUB R0,R6,R8
AND R0,R0,#3
STR R0,[R10,#mDIR%]
MOV R0,R7
BL wallfcc
LDMFD R13!,{R6-R8,PC}

\ InitLine into the projectile R7 - from R0 towards R1
.linec
STMFD R13!,{R6,R14}
MOV R6,R0
MOV R2,#256
MOV R3,#1
BL losc
STR R6,[R7,#pPOS%]
LDR R0,[R12,#36]
STR R0,[R7,#pDIAG%]
LDR R0,[R12,#28]
STR R0,[R7,#pSTEP1%]
LDR R0,[R12,#40]
STR R0,[R7,#pERR%]
LDR R0,[R12,#24]
STR R0,[R7,#pMIN%]
LDR R0,[R12,#32]
STR R0,[R7,#pMAJ%]
LDMFD R13!,{R6,PC}

\ CheckCombat - the Thing in R10 sees the man - arrows, lightning, fireballs
.combatc
STMFD R13!,{R6-R8,R14}
BL fpsc
CMP R0,#0
LDMEQFD R13!,{R6-R8,PC}
MOV R7,R0
LDR R0,[R10,#mPOS%]
LDR R1,[R12,#80]
MOV R2,#50
MOV R3,#0
BL losc
CMP R0,#0
BNE cbns
LDR R0,[R12,#20]
CMP R0,#0
BNE cbns
MOV R0,#1
STR R0,[R12,#92]
LDR R0,[R10,#mFLG%]
ORR R0,R0,#1
STR R0,[R10,#mFLG%]
LDR R8,[R12,#32]
LDR R0,[R12,#24]
CMP R0,#0
BNE cbmag
MOV R0,#40
BL rrc
CMP R0,R8
BCC cbmag
LDR R0,[R10,#mARR%]
CMP R0,#0
BEQ cbmag
SUB R0,R0,#1
STR R0,[R10,#mARR%]
MOV R0,#15
STR R0,[R7,#pCNT%]
STR R0,[R7,#pREL%]
LDR R0,[R10,#mPOS%]
STR R0,[R7,#pPOS%]
LDR R0,[R12,#28]
STR R0,[R7,#pDIAG%]
AND R0,R0,#1
EOR R0,R0,#&A1
STR R0,[R7,#pCHR%]
MOV R0,#55
STR R0,[R7,#pLIFE%]
MOV R0,#0
STR R0,[R7,#pBACK%]
STR R0,[R7,#pMANF%]
MVN R0,#0
STR R0,[R7,#pTGT2%]
MOV R0,#1
STR R0,[R7,#pKIND%]
STMFD R13!,{R10}
MOV R10,R7
BL arrowc
LDMFD R13!,{R10}
.cbmag
LDR R6,[R10,#mMAG%]
CMP R6,#6
LDMCCFD R13!,{R6-R8,PC}
MOV R0,#12
BL rrc
CMP R0,R6
LDMCSFD R13!,{R6-R8,PC}
BL rnd8c
AND R0,R0,#7
CMP R0,#2
LDMCSFD R13!,{R6-R8,PC}
CMP R0,#0
BNE cbfire
LDR R0,[R10,#mPOS%]
LDR R1,[R12,#80]
BL linec
MOV R0,#3
STR R0,[R7,#pKIND%]
MOV R0,#&A0
STR R0,[R7,#pCHR%]
MOV R0,#14
STR R0,[R7,#pCNT%]
STR R0,[R7,#pREL%]
MOV R0,#100
STR R0,[R7,#pLIFE%]
MOV R0,#0
STR R0,[R7,#pBACK%]
STR R0,[R7,#pMANF%]
MVN R0,#0
STR R0,[R7,#pTGT2%]
SUB R6,R6,#6
STR R6,[R10,#mMAG%]
LDMFD R13!,{R6-R8,PC}
.cbfire
CMP R6,#10
LDMCCFD R13!,{R6-R8,PC}
BL fpsc
CMP R0,#0
LDMEQFD R13!,{R6-R8,PC}
MOV R7,R0
LDR R0,[R10,#mPOS%]
LDR R1,[R12,#80]
BL linec
MOV R0,#2
STR R0,[R7,#pKIND%]
MOV R0,#&A4
STR R0,[R7,#pCHR%]
MOV R0,#24
STR R0,[R7,#pCNT%]
STR R0,[R7,#pREL%]
MOV R0,#50
STR R0,[R7,#pLIFE%]
MOV R0,#0
STR R0,[R7,#pBACK%]
STR R0,[R7,#pMANF%]
MVN R0,#0
STR R0,[R7,#pTGT%]
STR R0,[R7,#pTGT2%]
LDR R0,[R12,#80]
STR R0,[R7,#pTPOS%]
SUB R6,R6,#10
STR R6,[R10,#mMAG%]
LDMFD R13!,{R6-R8,PC}
.cbns
LDR R0,[R10,#mFLG%]
BIC R0,R0,#1
STR R0,[R10,#mFLG%]
LDMFD R13!,{R6-R8,PC}

\ MMons2 - one action of the Thing in R10
.monactc
STMFD R13!,{R6,R14}
LDR R0,[R10,#mDEAD%]
CMP R0,#0
BEQ ma1
LDR R1,[R10,#mCHR%]
CMP R1,#&E0
LDMCCFD R13!,{R6,PC}
ADD R1,R1,#1
AND R1,R1,#&E3
STR R1,[R10,#mCHR%]
LDR R0,[R10,#mPOS%]
MOV R2,#0
BL putc
LDMFD R13!,{R6,PC}
.ma1
LDR R0,[R10,#mFLG%]
TST R0,#1
BEQ ma2
LDR R0,[R10,#mCHR%]
CMP R0,#&D6
BNE ma4
BL rnd8c
AND R0,R0,#7
ORR R0,R0,#&D0
CMP R0,#&D6
MOVEQ R0,#&D7
STR R0,[R10,#mCHR%]
B ma4
.ma2
LDR R0,[R10,#mCHR%]
CMP R0,#&D6
BEQ ma4
BL rnd8c
LDR R1,[R12,#44]
MOV R1,R1,LSR #8
ORR R0,R0,R1
TST R0,#&3F
BNE ma4
LDR R0,[R10,#mPOS%]
BL visc
CMP R1,#0
MOVEQ R0,#&D6
STREQ R0,[R10,#mCHR%]
.ma4
LDR R0,[R10,#mCHR%]
CMP R0,#&D6
BEQ ma5
LDR R0,[R10,#mPOS%]
MOV R1,#&20
MOV R2,#0
BL putc
LDR R0,[R10,#mTYPE%]
CMP R0,#2
BCS ma6
BL mt1c
B ma5
.ma6
MOVEQ R0,#1
MVNNE R0,#0
BL mt2c
.ma5
BL combatc
LDR R0,[R10,#mPOS%]
LDR R1,[R10,#mCHR%]
MOV R2,#0
BL putc
LDMFD R13!,{R6,PC}

\ NextPoint - one step on the line of the projectile in R10
.nextc
LDR R0,[R10,#pERR%]
LDR R1,[R10,#pMIN%]
LDR R2,[R10,#pPOS%]
SUBS R0,R0,R1
LDRMI R1,[R10,#pDIAG%]
LDRMI R3,[R10,#pMAJ%]
ADDMI R0,R0,R3
LDRPL R1,[R10,#pSTEP1%]
STR R0,[R10,#pERR%]
ADD R2,R2,R1
MOV R2,R2,LSL #18
MOV R2,R2,LSR #18
STR R2,[R10,#pPOS%]
MOV PC,R14

\ MoveArrow
.arrowc
STMFD R13!,{R14}
LDR R0,[R10,#pPOS%]
LDR R1,[R10,#pDIAG%]
ADD R0,R0,R1
MOV R0,R0,LSL #18
MOV R0,R0,LSR #18
STR R0,[R10,#pPOS%]
LDRB R1,[R11,R0]
CMP R1,#&20
LDMEQFD R13!,{PC}
AND R2,R1,#&FE
CMP R2,#&DC
BEQ arend
AND R2,R1,#&F0
CMP R2,#&90
BEQ arend
AND R2,R1,#&F8
CMP R2,#&D0
BNE ar3
MOV R2,#0
STR R2,[R10,#pLIFE%]
MOV R1,#70
MOV R2,#&DF
BL hitmonc
LDMFD R13!,{PC}
.ar3
CMP R2,#&A8
LDMNEFD R13!,{PC}
LDR R0,[R10,#pLIFE%]
ADD R0,R0,#25
MOV R0,R0,ASR #1
MOV R1,#0
STR R1,[R10,#pLIFE%]
BL hitmanc
LDMFD R13!,{PC}
.arend
MOV R2,#0
STR R2,[R10,#pLIFE%]
LDMFD R13!,{PC}

\ MFire1 - the fireball ends, its Thing can be a target again
.mfire1c
MOV R0,#0
STR R0,[R10,#pLIFE%]
LDR R0,[R10,#pTGT2%]
CMP R0,#0
MOVLT PC,R14
LDR R1,[R12,#72]
ADD R1,R1,R0,LSL #6
LDR R0,[R1,#mFLG%]
BIC R0,R0,#&80
STR R0,[R1,#mFLG%]
MOV PC,R14

\ MoveFire - it follows its target
.firec
STMFD R13!,{R6,R7,R14}
LDR R0,[R10,#pCHR%]
ADD R0,R0,#1
AND R0,R0,#3
ORR R0,R0,#&A4
STR R0,[R10,#pCHR%]
LDR R0,[R10,#pTGT%]
CMP R0,#0
LDRLT R1,[R12,#80]
LDRGE R1,[R12,#72]
ADDGE R1,R1,R0,LSL #6
LDRGE R1,[R1,#mPOS%]
LDR R0,[R10,#pTPOS%]
CMP R0,R1
BEQ fi1
STR R1,[R10,#pTPOS%]
MOV R7,R10
LDR R0,[R10,#pPOS%]
BL linec
.fi1
BL nextc
LDR R0,[R10,#pPOS%]
LDRB R1,[R11,R0]
CMP R1,#&20
LDMEQFD R13!,{R6,R7,PC}
AND R2,R1,#&FE
CMP R2,#&DC
BEQ fiend
AND R2,R1,#&F0
CMP R2,#&90
BEQ fiend
AND R2,R1,#&F8
CMP R2,#&D0
BNE fi3
BL mfire1c
LDR R0,[R10,#pPOS%]
MOV R1,#40
MOV R2,#&E0
BL hitmonc
LDMFD R13!,{R6,R7,PC}
.fi3
CMP R2,#&A8
LDMNEFD R13!,{R6,R7,PC}
LDR R0,[R10,#pMANF%]
CMP R0,#0
BEQ fi4
LDR R0,[R10,#pLIFE%]
RSB R0,R0,#50
CMP R0,#4
LDMCCFD R13!,{R6,R7,PC}
.fi4
BL mfire1c
MOV R0,#15
BL hitmanc
LDMFD R13!,{R6,R7,PC}
.fiend
BL mfire1c
LDMFD R13!,{R6,R7,PC}

\ wall or door? R0 position, Z set if so (uses R1)
.twc
MOV R0,R0,LSL #18
MOV R0,R0,LSR #18
LDRB R1,[R11,R0]
AND R0,R1,#&FE
CMP R0,#&DC
MOVEQ PC,R14
AND R0,R1,#&F0
CMP R0,#&90
MOV PC,R14

\ MLightRefl - back one step, turn the part of the motion that hits
.reflc
STMFD R13!,{R6-R8,R14}
LDR R0,[R10,#pBERR%]
STR R0,[R10,#pERR%]
LDR R6,[R10,#pBPOS%]
STR R6,[R10,#pPOS%]
LDR R1,[R10,#pSTEP1%]
ADD R0,R6,R1
BL twc
LDREQ R1,[R10,#pSTEP1%]
RSBEQ R1,R1,#0
STREQ R1,[R10,#pSTEP1%]
LDR R0,[R10,#pMIN%]
CMP R0,#0
LDMEQFD R13!,{R6-R8,PC}
LDR R7,[R10,#pDIAG%]
MOV R8,#0
CMP R7,#0
MVNLT R2,#127
MOVGE R2,#128
TST R7,#64
MVNNE R3,#0
MOVEQ R3,#1
ADD R0,R6,R2
BL twc
SUBEQ R7,R7,R2,LSL #1
MOVEQ R8,#1
ADD R0,R6,R3
BL twc
SUBEQ R7,R7,R3,LSL #1
MOVEQ R8,#1
CMP R8,#0
RSBEQ R7,R7,#0
LDREQ R1,[R10,#pSTEP1%]
RSBEQ R1,R1,#0
STREQ R1,[R10,#pSTEP1%]
STR R7,[R10,#pDIAG%]
LDMFD R13!,{R6-R8,PC}

\ MoveLight - bounces on walls and doors, goes through what it hits
.lightc
STMFD R13!,{R6,R14}
LDR R0,[R10,#pCHR%]
ADD R0,R0,#1
AND R0,R0,#1
ORR R0,R0,#&A2
STR R0,[R10,#pCHR%]
LDR R0,[R10,#pERR%]
STR R0,[R10,#pBERR%]
LDR R0,[R10,#pPOS%]
STR R0,[R10,#pBPOS%]
MOV R6,#0
.li1
BL nextc
LDR R0,[R10,#pPOS%]
BL twc
BNE li2
BL reflc
ADD R6,R6,#1
CMP R6,#5
BCC li1
LDR R0,[R10,#pBPOS%]
STR R0,[R10,#pPOS%]
LDMFD R13!,{R6,PC}
.li2
LDR R0,[R10,#pPOS%]
LDRB R1,[R11,R0]
CMP R1,#&20
LDMEQFD R13!,{R6,PC}
AND R2,R1,#&F8
CMP R2,#&D0
BNE li3
MOV R1,#40
MOV R2,#&E0
BL hitmonc
LDMFD R13!,{R6,PC}
.li3
CMP R2,#&A8
LDMNEFD R13!,{R6,PC}
LDR R0,[R10,#pMANF%]
CMP R0,#0
BEQ li4
LDR R0,[R10,#pLIFE%]
RSB R0,R0,#100
CMP R0,#4
LDMCCFD R13!,{R6,PC}
.li4
MOV R0,#10
BL hitmanc
LDMFD R13!,{R6,PC}

\ MProj2A..MProj4 - one step of the projectile in R10
.pstepc
STMFD R13!,{R14}
LDR R0,[R10,#pLIFE%]
SUBS R0,R0,#1
MOVMI R0,#0
STR R0,[R10,#pLIFE%]
BGT ps1
LDR R0,[R10,#pBACK%]
CMP R0,#0
LDMEQFD R13!,{PC}
LDR R0,[R10,#pPOS%]
MOV R1,#&20
MOV R2,#0
BL putc
LDMFD R13!,{PC}
.ps1
LDR R0,[R10,#pBACK%]
CMP R0,#0
BEQ ps2
LDR R0,[R10,#pPOS%]
MOV R1,#&20
MOV R2,#0
BL putc
.ps2
LDR R0,[R10,#pKIND%]
CMP R0,#1
BNE ps3
BL arrowc
B ps5
.ps3
CMP R0,#2
BNE ps4
BL firec
B ps5
.ps4
BL lightc
.ps5
LDR R0,[R10,#pLIFE%]
CMP R0,#0
LDMEQFD R13!,{PC}
LDR R0,[R10,#pPOS%]
LDRB R1,[R11,R0]
CMP R1,#&20
MOVNE R1,#0
STRNE R1,[R10,#pBACK%]
LDMNEFD R13!,{PC}
LDR R1,[R10,#pCHR%]
MOV R2,#0
BL putc
MOV R1,#1
STR R1,[R10,#pBACK%]
LDMFD R13!,{PC}
.pstep%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R11,[R12,#0]
MOV R10,R0
BL pstepc
LDMFD R13!,{R4-R12,PC}

\ GameLoop without the man - R0 cycles of MoveMonsters and MoveProj
.run%
STMFD R13!,{R4-R12,R14}
MOV R12,R7
LDR R11,[R12,#0]
MOV R8,R0
.rn1
SUBS R8,R8,#1
BMI rnx
LDR R10,[R12,#72]
MOV R9,#32
.rn2
LDR R0,[R10,#mCNT%]
SUB R0,R0,#1
AND R0,R0,#255
STR R0,[R10,#mCNT%]
CMP R0,#0
BNE rn3
LDR R0,[R10,#mREL%]
STR R0,[R10,#mCNT%]
STMFD R13!,{R8,R9}
BL monactc
LDMFD R13!,{R8,R9}
LDR R0,[R12,#96]
CMP R0,#0
BNE rnx
.rn3
ADD R10,R10,#64
SUBS R9,R9,#1
BNE rn2
LDR R10,[R12,#76]
MOV R9,#32
.rn4
LDR R0,[R10,#pLIFE%]
CMP R0,#0
BEQ rn5
LDR R0,[R10,#pCNT%]
SUBS R0,R0,#1
STR R0,[R10,#pCNT%]
BNE rn5
LDR R0,[R10,#pREL%]
STR R0,[R10,#pCNT%]
STMFD R13!,{R8,R9}
BL pstepc
LDMFD R13!,{R8,R9}
LDR R0,[R12,#96]
CMP R0,#0
BNE rnx
.rn5
ADD R10,R10,#128
SUBS R9,R9,#1
BNE rn4
B rn1
.rnx
LDMFD R13!,{R4-R12,PC}
]
NEXT
ENDPROC

REM ================= small helpers =================
REM write a cell of the map and of the screen (OutIt + SCV)
DEF PROCput(p%,c%):A%=p%:B%=c%:C%=0:CALL put%:ENDPROC
REM only on the screen
DEF PROCscr(p%,c%):A%=p%:B%=c%:C%=1:CALL put%:ENDPROC
DEF PROCframe:ws%!4=FX%:ws%!8=FY%:ENDPROC
DEF PROCnewscreen:PROCframe:CALL redraw%:ENDPROC
DEF FNvis(p%)=((((p% AND 127)-FX%) AND 255)<32) AND ((((p%>>7)-FY%) AND 255)<24)
DEF FNrand=USR(rand%)
DEF FNrr(n%):A%=n%:=USR(rr%)
REM the Z80 refresh register: a "poor" random byte
DEF FNr8=RND(256)-1
DEF FNrk(k%):IF KP%(k%) THEN KP%(k%)=FALSE:=TRUE
=FALSE
DEF FNfps=USR(fps%)
DEF PROCtx(x%,y%,t$)
LOCAL i%
FOR i%=1 TO LEN(t$)
  A%=x%+i%-1:B%=y%:C%=ASC(MID$(t$,i%,1)):CALL plot%
NEXT
ENDPROC
DEF PROCkeymap
LOCAL k%
FOR k%=0 TO 15
  IF KS% THEN KN%(k%)=KM%(k%) ELSE KN%(k%)=KO%(k%)
NEXT
ENDPROC
DEF PROCscankeys
LOCAL k%,d%
FOR k%=0 TO 15
  d%=FALSE:IF KN%(k%) THEN d%=INKEY(KN%(k%))
  IF d% AND NOT KW%(k%) THEN KP%(k%)=TRUE
  KW%(k%)=d%
NEXT
ENDPROC
DEF PROCclearkeys
LOCAL k%
FOR k%=0 TO 15
  KP%(k%)=FALSE:KW%(k%)=FALSE:IF KN%(k%) THEN KW%(k%)=INKEY(KN%(k%))
NEXT
ENDPROC

REM ================= the maze (Main, Page: in the assembler) =================
DEF PROCmaze(f%):A%=f%:CALL maze%:ENDPROC
DEF PROCpage(f%):A%=f%:CALL page%:ENDPROC

REM InitSanctuary: columns 113-127, drawn once per game
DEF PROCsanctuary
LOCAL k%,j%,a%,r%,c%,m$
A%=M%:B%=16384:C%=&20:CALL fill%
a%=113:M%?a%=&95
FOR k%=1 TO 13:M%?(a%+k%)=&93:NEXT
M%?127=&96
FOR k%=1 TO 111:M%?(k%*128+127)=&9C:NEXT
M%?(112*128+127)=&9A
FOR k%=114 TO 126:M%?(112*128+k%)=&93:NEXT
M%?(112*128+113)=&99
FOR k%=99 TO 111:M%?(k%*128+113)=&9C:NEXT
r%=99
FOR j%=1 TO 7
  M%?((r%-1)*128+113)=&9C:M%?((r%-2)*128+113)=&94
  FOR k%=3 TO 6:M%?((r%-k%)*128+113)=&90:NEXT
  M%?((r%-7)*128+113)=&98
  r%-=7
NEXT
FOR k%=1 TO 49:M%?(k%*128+113)=&9C:NEXT
M%?(50*128+113)=&99
M%?(22*128+113)=&9D
FOR k%=114 TO 126:M%?(22*128+k%)=&93:NEXT
M%?(22*128+127)=&9E
FOR k%=114 TO 118:M%?(50*128+k%)=&93:NEXT
M%?(50*128+119)=&96
FOR k%=51 TO 105:M%?(k%*128+119)=&9C:NEXT
M%?(106*128+119)=&98
REM the stairs: a landing, three rows of the floor marker, three steps
r%=51
FOR c%=&87 TO &80 STEP -1
  PROCrung(r%,&FC):r%+=1
  FOR k%=1 TO 3:PROCrung(r%,c%):r%+=1:NEXT
  FOR k%=1 TO 3:PROCrung(r%,&FD):r%+=1:NEXT
NEXT
FOR k%=1 TO 5:PROCrung(r%,&FC):r%+=1:NEXT
RESTORE +1
FOR k%=0 TO 15
  READ m$
  FOR j%=1 TO 11:M%?((k%+2)*128+115+j%-1)=ASC(MID$(m$+"           ",j%,1)):NEXT
NEXT
DATA "  Halls","  Of","  The","  Things","","Things","trained by",""
DATA "Martin","Horsley","","Neil","Mottershead","","Simon","Brattel"
ENDPROC
DEF PROCrung(r%,c%)
LOCAL k%
FOR k%=114 TO 118:M%?(r%*128+k%)=c%:NEXT
ENDPROC

REM ================= the floors (Gen8, SaveFloor, Recreate) =================
REM FindPosition: a random empty cell in rows and columns 0-111
DEF FNfindpos=USR(fpos%)

REM InitMonsters: 32 Things from MonsterTypes (base + RandRange)
DEF PROCinitmon(f%)
LOCAL i%,k%,b%,m%
A%=IB%:CALL imon%
FOR i%=0 TO 31
  b%=IB%+i%*32:m%=ML%+i%*64
  FOR k%=0 TO 60 STEP 4:m%!k%=0:NEXT
  m%!mCHR%=!b%:m%!mTYPE%=b%!4:m%!mMAG%=b%!8:m%!mMAX%=b%!12
  m%!mREL%=b%!16:m%!mARR%=b%!20:m%!mHP%=b%!24:m%!mPOS%=b%!28
NEXT
IF f%=0 THEN ENDPROC
FOR i%=0 TO f%-1:ML%!(i%*64+mMAG%)=255:ML%!(i%*64+mREL%)=30:NEXT
ML%!((f%-1)*64+mHP%)=255
ENDPROC

DEF PROCinittreasure:CALL itre%:ENDPROC

DEF PROCgen8
LOCAL f%,k%,p%
RF%(0)=0
FOR k%=1 TO 7:RF%(k%)=FNrr(7)+1:NEXT
ws%!88=0:NR%=0
FOR f%=0 TO 7
  CF%=f%
  FSD%(f%)=ws%!44
  PROCmaze(f%):PROCpage(f%):PROCinitmon(f%):PROCinittreasure
  PROCsavefloor(f%)
  FOR k%=0 TO 7
    IF RF%(k%)=f% THEN
      REPEAT p%=FNfindpos:RP%(k%)=p%:M%?p%=&88:UNTIL (p% AND 127)<30
    ENDIF
  NEXT
NEXT
CF%=8
ENDPROC

DEF PROCsavefloor(f%)
LOCAL i%,n%,m%
FOR i%=0 TO 31
  m%=ML%+i%*64
  FSP%(f%,i%)=m%!mPOS%:FSC%(f%,i%)=m%!mCHR%:FSF%(f%,i%)=(m%!mDEAD%<<7) OR m%!mTYPE%
NEXT
A%=FT%+f%*4096:B%=0:n%=USR(scan%)
IF n%>1023 THEN n%=1023
FTN%(f%)=n%
ENDPROC

REM OpenIfNess: open the door at p% (code c%)
DEF PROCopenif(p%,c%)
LOCAL d%,k%
d%=0
IF c%=&DC THEN d%=1
IF c%=&DD THEN d%=128
IF d%=0 THEN ENDPROC
REPEAT p%+=d%:UNTIL (M%?p% AND &FE)<>&DC
FOR k%=1 TO 4:p%-=d%:PROCput(p%,&20):NEXT
ENDPROC

DEF PROCrecreate(f%)
LOCAL i%,p%,e%,a%,m%
ws%!44=FSD%(f%)
PROCmaze(f%):PROCpage(f%):PROCinitmon(f%)
FOR i%=0 TO 31
  p%=FSP%(f%,i%):m%=ML%+i%*64
  IF M%?p%<>&20 THEN PROCopenif(p%,M%?p%)
  m%!mPOS%=p%:m%!mCHR%=FSC%(f%,i%):m%!mTYPE%=FSF%(f%,i%) AND 3
  IF FSF%(f%,i%) AND &80 THEN m%!mDEAD%=1
NEXT
a%=FT%+f%*4096
FOR i%=0 TO FTN%(f%)-1
  e%=a%!(i%*4):p%=e%>>8
  IF M%?p%<>&20 THEN PROCopenif(p%,M%?p%)
  M%?p%=e% AND &FF
NEXT
FOR i%=0 TO 31:PL%!(i%*128+pLIFE%)=0:NEXT
FOR i%=0 TO 7
  IF RF%(i%)=CF% THEN M%?RP%(i%)=&88+i%
NEXT
ws%!92=0
ENDPROC

DEF PROCplacemon
LOCAL i%
FOR i%=0 TO 31:M%?(ML%!(i%*64+mPOS%))=ML%!(i%*64+mCHR%):NEXT
ENDPROC

REM NFloor: the man stands on the marker of another floor
DEF PROCnfloor
LOCAL b%
b%=NF%-1
IF CF%=8 THEN ENDPROC
IF (CF% AND 7)=b% THEN ENDPROC
PROCsavefloor(CF% AND 7)
CF%=b%
PROCrecreate(CF%)
PROCplacemon
PROCnewscreen
PROCpm
ENDPROC

REM ================= the man =================
DEF PROCpm
LOCAL c%
c%=&A8+2*MA%
IF M%?PM%<&80 THEN M%?PM%=c%:M%?(PM%+128)=c%+1
PROCscr(PM%,c%):PROCscr(PM%+128,c%+1)
ENDPROC

REM the man in the middle of the screen (2 cells tall: rows 11 and 12)
DEF PROCfollow
FX%=((PM% AND 127)-16) AND 255:FY%=((PM%>>7)-11) AND 255
PROCnewscreen
PROCpm
ENDPROC

REM JustifyT: line the rooms up on the screen (SPACE)
DEF PROCjustify(force%)
LOCAL nx%,ny%
nx%=(7*((((PM% AND 127)-1) AND 255) DIV 7)-8) AND 255
ny%=(7*((PM%>>7) DIV 7)-8) AND 255
IF NOT force% AND nx%=FX% AND ny%=FY% THEN ENDPROC
FX%=nx%:FY%=ny%
PROCnewscreen
PROCpm
ENDPROC

DEF FNmanvalid(c%)
IF c%=&20 THEN =TRUE
IF c%=&90 THEN =FNopenplan
IF (c% AND &FE)=&FC THEN =TRUE
IF (c% AND &F8)=&80 THEN =TRUE
=FALSE

REM OpenPlan: the force field; on floor 0 it is the way out, and the end
DEF FNopenplan
IF (CF% AND 7)<>0 THEN =TRUE
IF ws%!92=0 THEN =TRUE
IF CF%=8 THEN ET%=1
=FALSE

DEF PROCman
LOCAL d%,l%,r%,u%,w%,n%,c%
IF KS%=0 AND INKEY(-17) AND INKEY(-36) AND INKEY(-54) AND INKEY(-38) THEN ET%=2:ENDPROC
IF KS% AND INKEY(-113) THEN ET%=2:ENDPROC
PROCmdoor
PROCkeepdrop
IF M%?PM%<>&90 THEN
  PROCmfirearrow
  IF ET%>=0 THEN ENDPROC
  PROCfireball
  PROClightning
  IF FNsword THEN ENDPROC
ENDIF
KP%(4)=FALSE:KP%(5)=FALSE:KP%(6)=FALSE:KP%(7)=FALSE
d%=-1
l%=INKEY(-98) OR INKEY(-26):r%=INKEY(-67) OR INKEY(-122)
IF l% AND NOT r% THEN d%=3
IF r% AND NOT l% THEN d%=1
IF d%<0 THEN
  u%=INKEY(-86) OR INKEY(-58):w%=INKEY(-102) OR INKEY(-42)
  IF u% AND NOT w% THEN d%=0
  IF w% AND NOT u% THEN d%=2
ENDIF
IF d%<0 THEN ENDPROC
IF d%<>MA% THEN MA%=d%:MN%=T%+30:PROCpm:ENDPROC
IF UM%=0 THEN M%?PM%=&20:M%?(PM%+128)=&20
PROCscr(PM%,M%?PM%):PROCscr(PM%+128,M%?(PM%+128))
n%=PM%+CD%(MA%)
NF%=0
IF NOT FNmanvalid(M%?n%) THEN PROCpm:ENDPROC
IF ET%>=0 THEN ENDPROC
c%=M%?(n%+128)
IF NOT FNmanvalid(c%) THEN PROCpm:ENDPROC
IF ET%>=0 THEN ENDPROC
IF c%>=&80 AND c%<&88 THEN NF%=(c% AND 7)+1
UM%=c% EOR &20
PM%=n%:ws%!80=PM%
IF FOL% THEN
  PROCfollow
  IF NF% THEN PROCnfloor
  ENDPROC
ENDIF
IF FNvis(PM%) AND FNvis(PM%+128) THEN
  PROCpm
  IF NF% THEN PROCnfloor
ELSE
  REM MManNewFrame: the screen jumps by 28 columns or 21 rows
  CASE MA% OF
  WHEN 0:FY%=(FY%-21) AND 255
  WHEN 1:FX%=(FX%+28) AND 255
  WHEN 2:FY%=(FY%+21) AND 255
  OTHERWISE:FX%=(FX%-28) AND 255
  ENDCASE
  PROCnewscreen
  PROCpm
ENDIF
ENDPROC

REM MMoveDoor: O opens the door in front, C closes a gap or an open door
DEF PROCmdoor
LOCAL h%,d%,k%,c%,cl%
cl%=FNrk(1)
IF FNrk(0) THEN
  h%=PM%+CD%(MA%):IF MA%=2 THEN h%+=CD%(MA%)
  REM modern keys: the same key closes when there is no door in front
  IF KS% AND (M%?h% AND &FE)<>&DC THEN cl%=TRUE
ENDIF
IF NOT cl% AND h%<>0 THEN
  IF (M%?h% AND &FE)<>&DC THEN ENDPROC
  d%=CD%((MA%+1) AND 3)
  REPEAT h%+=d%:UNTIL (M%?h% AND &FE)<>&DC
  FOR k%=1 TO 4:h%-=d%:PROCput(h%,&20):NEXT
  ENDPROC
ENDIF
IF NOT cl% THEN ENDPROC
h%=PM%+CD%(MA%):IF MA%=2 THEN h%+=CD%(MA%)
d%=CD%((MA%+1) AND 3)
IF M%?h%<>&20 THEN ENDPROC
REPEAT h%+=d%:UNTIL M%?h%<>&20
IF (M%?h% AND &F0)<>&90 THEN ENDPROC
FOR k%=1 TO 4
  h%-=d%:IF M%?h%<>&20 THEN ENDPROC
NEXT
h%-=d%
IF (M%?h% AND &F0)<>&90 THEN ENDPROC
IF d%=1 OR d%=-1 THEN c%=&DC ELSE c%=&DD
FOR k%=1 TO 4:h%+=d%:PROCput(h%,c%):NEXT
ENDPROC

REM MMKeepDrop: K keeps what is in front, D drops the last treasure
DEF PROCkeepdrop
LOCAL h%
IF FNrk(3) THEN
  IF TC%=0 THEN ENDPROC
  h%=PM%+CD%(MA%):IF MA%=2 THEN h%+=CD%(MA%)
  IF M%?h%<>&20 THEN
    IF (MA% AND 1)=0 THEN ENDPROC
    h%+=128:IF M%?h%<>&20 THEN ENDPROC
  ENDIF
  PROCput(h%,SB%(TC%)):TC%-=1
  ENDPROC
ENDIF
IF NOT FNrk(2) THEN ENDPROC
h%=PM%+CD%(MA%):IF MA%=2 THEN h%+=CD%(MA%)
IF FNkeep2(h%) THEN IF MA% AND 1 THEN h%=FNkeep2(h%+128)
ENDPROC

REM MMKeep2: TRUE when there was nothing to keep (try the feet row)
DEF FNkeep2(h%)
LOCAL c%,n%
c%=M%?h%
IF (c% AND &F0)=&B0 THEN
  IF TC%=5 THEN =FALSE
  TC%+=1:SB%(TC%)=c%:PROCput(h%,&20)
  =FALSE
ENDIF
IF (c% AND &F0)=&C0 THEN
  n%=MG%+2*(c% AND 15)
  IF n%>255 THEN =TRUE
  IF n%=255 THEN =FALSE
  MG%=n%:PROCput(h%,&20)
  =FALSE
ENDIF
IF (c% AND &FC)=&D8 THEN PROCput(h%,&20):AR%=(AR%+1) AND 255:=FALSE
IF (c% AND &F8)=&88 THEN PROCring(h%,c%):=FALSE
=TRUE

REM MMKRing: a ring or the golden key
DEF PROCring(h%,c%)
LOCAL k%
RF%(c% AND 7)=255
PROCput(h%,&20)
IF c%=&88 THEN CF%=8
NR%+=1
IF NR%<>7 THEN ENDPROC
REM GoldKey: the door of floor 0 opens in the sanctuary
FOR k%=0 TO 5:PROCput((104-k%)*128+113,M%?((97-k%)*128+113)):NEXT
ENDPROC

REM MSword: five phases, one per step; TRUE while the man must stand
DEF FNsword
LOCAL e%,c%
IF SP%=0 THEN
  IF NOT FNrk(4) THEN =FALSE
  SP%=6:SW%=PM%+CD%(MA%):IF MA%=2 THEN SW%+=CD%(MA%)
ENDIF
e%=6-SP%
PROCscr(SW%,M%?SW%)
SP%-=1
IF SP%=0 THEN =FALSE
SW%+=SWO%(MA%,e%)
c%=M%?SW%
IF (c% AND &FE)=&DC THEN SP%=0:=FALSE
IF (c% AND &F8)=&D0 THEN SP%=0:PROChitmon(SW%,15,&DE):=FALSE
IF (c% AND &F0)=&90 THEN SP%=0:=FALSE
PROCscr(SW%,SWC%(MA%,e%))
=TRUE

REM MFireArrow: the arrow leaves from the head or the feet, towards the
REM nearest Thing in front (the scan of the Z80, with its asymmetry)
DEF PROCmfirearrow
LOCAL j%,h%,de%,ho%,fo%,bl%,k%,n%,c%,res%
IF NOT FNrk(5) THEN ENDPROC
j%=FNfps:IF j%=0 THEN ENDPROC
IF AR%=0 THEN ENDPROC
AR%-=1
IF KS% THEN AD%=MA%
h%=PM%:de%=CD%(AD%)
IF AD%=2 THEN h%+=de%
IF AD%=1 OR AD%=3 THEN
  ho%=-128:fo%=0:bl%=0:k%=PM%+128:res%=0:n%=0
  REPEAT
    k%+=de%:n%+=1
    c%=M%?((k%+ho%) AND &3FFF)
    IF (c% AND &FE)=&DC OR (c% AND &F8)=&D0 THEN res%=2
    IF res%=0 AND (c% AND &F0)=&90 THEN
      IF bl% THEN res%=2
      bl%=1:ho%=0
    ENDIF
    IF res%=0 THEN
      c%=M%?((k%+fo%) AND &3FFF)
      IF (c% AND &FE)=&DC THEN res%=2
      IF res%=0 AND (c% AND &F8)=&D0 THEN res%=1
      IF res%=0 AND (c% AND &F0)=&90 THEN
        IF bl% THEN res%=2
        bl%=1:fo%=-128
      ENDIF
    ENDIF
  UNTIL res%>0 OR n%=256
  IF res%=1 THEN h%+=128
ENDIF
j%!pKIND%=1:j%!pDIAG%=de%:j%!pPOS%=h%:j%!pBACK%=0:j%!pLIFE%=32
IF AD% AND 1 THEN j%!pCHR%=&A0 ELSE j%!pCHR%=&A1
j%!pCNT%=5:j%!pREL%=5:j%!pMANF%=1:j%!pTGT2%=-1
A%=j%:CALL pstep%
ENDPROC

REM InitLine: projectile j% goes from s% towards t%
DEF PROCinitline(j%,t%,s%)
A%=s%:B%=t%:C%=256:D%=1:t%=USR(los%)
j%!pPOS%=s%:j%!pDIAG%=ws%!36:j%!pSTEP1%=ws%!28:j%!pERR%=ws%!40:j%!pMIN%=ws%!24:j%!pMAJ%=ws%!32
ENDPROC

REM MonsterSearch: the nearest Thing in sight, not already targeted
DEF FNmonsearch(c%)
LOCAL i%,b%,r%,best%,m%
b%=-1:best%=255
FOR i%=0 TO 31
  m%=ML%+i%*64
  IF m%!mDEAD%=0 AND (m%!mFLG% AND &80)=0 THEN
    A%=PM%:B%=m%!mPOS%:C%=c%:D%=0:r%=USR(los%)
    IF r%=0 AND ws%!20=0 THEN IF ws%!32<=best% THEN best%=ws%!32:b%=i%
  ENDIF
NEXT
=b%

DEF PROCfireball
LOCAL j%,i%,m%
IF NOT FNrk(6) THEN ENDPROC
j%=FNfps:IF j%=0 THEN ENDPROC
IF MG%<2 THEN ENDPROC
MG%-=2
i%=FNmonsearch(40):IF i%<0 THEN ENDPROC
m%=ML%+i%*64:m%!mFLG%=m%!mFLG% OR &80
PROCinitline(j%,m%!mPOS%,PM%)
j%!pKIND%=2:j%!pCHR%=&A4:j%!pCNT%=24:j%!pREL%=24:j%!pBACK%=0:j%!pLIFE%=50
j%!pMANF%=1:j%!pTGT%=i%:j%!pTPOS%=m%!mPOS%:j%!pTGT2%=i%
ENDPROC

DEF PROClightning
LOCAL j%,i%
IF NOT FNrk(7) THEN ENDPROC
j%=FNfps:IF j%=0 THEN ENDPROC
IF MG%<1 THEN ENDPROC
MG%-=1
i%=FNmonsearch(50):IF i%<0 THEN ENDPROC
PROCinitline(j%,ML%!(i%*64+mPOS%),PM%)
j%!pKIND%=3:j%!pCHR%=&A2:j%!pCNT%=14:j%!pREL%=14:j%!pBACK%=0:j%!pLIFE%=100
j%!pMANF%=1:j%!pTGT2%=-1
ENDPROC

REM HitMonster: the first Thing at p% takes d% wounds, c% is its corpse
DEF PROChitmon(p%,d%,c%):A%=p%:B%=d%:C%=c%:CALL hitmon%:ENDPROC

REM ================= the game =================
DEF PROCgame
LOCAL t0%,due%,n%,i%,j%,e%,g%,sT%,sK%,sF%
CLS
PROCtx(0,23,"Please wait for a second")
TK%=0:T%=0
PROCsanctuary
REM InitGame
FX%=255:FY%=255:TC%=0:MG%=120:AR%=255:UM%=0
MA%=1:AD%=1:SP%=0:ET%=-1
ws%!84=0:ws%!88=0:ws%!92=0:ws%!96=0
g%=TIME
PROCgen8
g%=TIME-g%
CLS
IF DBG% THEN
  PRINT TAB(0,31);"gen8 ";g%/100;" s";
  FOR i%=0 TO 7
    CF%=i%:PROCrecreate(i%):PROCplacemon
    OSCLI "SAVE "+HD$+"Map"+STR$i%+" "+STR$~M%+" +4000"
  NEXT
ENDIF
REM Telep0
CF%=0
PROCrecreate(0)
PROCplacemon
PM%=109*128+125:ws%!80=PM%
FX%=125:FY%=109
IF FOL% THEN PROCfollow ELSE PROCjustify(TRUE)
MN%=TK%+1
PROCkeymap
PROCclearkeys
PROCstats(TRUE)
t0%=TIME:sT%=TIME:sK%=TK%:sF%=0
REPEAT
  SYS "OS_Byte",15,1
  PROCscankeys
  IF DBG% THEN
    sF%+=1
    IF TIME-sT%>=100 THEN PRINT TAB(0,31);"cycles/s ";(TK%-sK%)*100 DIV (TIME-sT%);"  frames/s ";sF%*100 DIV (TIME-sT%);"   ";:sT%=TIME:sK%=TK%:sF%=0
  ENDIF
  due%=(TIME-t0%)*TR% DIV 100
  IF due%>TK%+40 THEN due%=TK%+40:t0%=TIME-due%*100 DIV TR%
  IF due%>TK% THEN
    REM GameLoop: the man, then the Things and the projectiles (assembler)
    WHILE TK%<due% AND ET%<0
      IF MN%<=TK% THEN T%=TK%:MN%=TK%+10:PROCman
      n%=due%-TK%:IF MN%-TK%<n% THEN n%=MN%-TK%
      IF n%<1 THEN n%=1
      IF ET%<0 THEN A%=n%:CALL run%
      TK%+=n%
      IF ws%!96 THEN ET%=0
    ENDWHILE
  ELSE
    SYS "OS_Byte",19
  ENDIF
  IF ET%<0 THEN
    REM JustifyScreen, TryHealing, DisplayScore, ArrowDir
    IF FNrk(10) THEN
      IF INKEY(-1) THEN FOL%=NOT FOL%
      IF FOL% THEN PROCfollow ELSE PROCjustify(FALSE)
    ENDIF
    IF FNrk(8) THEN PROCheal
    IF FNrk(9) THEN PROCstatus:t0%=TIME-TK%*100 DIV TR%:PROCstats(TRUE)
    IF FNrk(15) THEN STS%=NOT STS%:PROCstats(TRUE)
    PROCstats(FALSE)
    IF FNrk(14) THEN AD%=1
    IF FNrk(11) THEN AD%=3
    IF FNrk(13) THEN AD%=0
    IF FNrk(12) THEN AD%=2
  ENDIF
UNTIL ET%>=0
ENDPROC

DEF PROCheal
IF MG%<30 THEN ENDPROC
MG%-=30:ws%!84-=(FNr8 AND 15)+16
IF ws%!84<0 THEN ws%!84=0
ENDPROC

REM DisplayScore: the status screen (key 1), the game stands still
DEF PROCstatus
LOCAL k%,l%
CLS
REPEAT
  PROCtx(0,0," -- Halls of the Things --")
  PROCtx(0,1,"___________________________")
  PROCtx(0,4,"(c) Design-Design Software")
  PROCtx(0,6,"    Simon Brattel")
  PROCtx(0,7,"    Neil Mottershead")
  PROCtx(0,8," &  Martin Horsley")
  PROCtx(15,18,"100%          0%")
  PROCtx(1,19,"Magic"):PROCbar(19,MG%>>1)
  PROCtx(1,20,"Wounds"):PROCbar(20,ws%!84)
  PROCtx(1,21,"Dead Things"):PROCbar(21,ws%!88>>1)
  PROCtx(1,22,"Arrows"):PROCbar(22,AR%>>1)
  PROCtx(1,15,"Rings found")
  l%=15
  FOR k%=7 TO 0 STEP -1
    IF RF%(k%)=255 THEN A%=l%:B%=15:C%=&88+k%:CALL plot%:l%+=1
  NEXT
  SYS "OS_Byte",19
  PROCscankeys
  IF FNrk(8) THEN PROCheal
UNTIL FNrk(9)
CLS
PROCnewscreen
PROCpm
ENDPROC

REM PrintBar: 0-127, fills from the right (0%) to the left (100%)
DEF PROCbar(y%,v%):PROCbarx(15,y%,v%):ENDPROC
DEF PROCbarx(x0%,y%,v%)
LOCAL x%,n%
IF v%>127 THEN v%=127
A%=x0%-1:B%=y%:C%=&F1:CALL plot%
A%=x0%+16:B%=y%:C%=&F2:CALL plot%
x%=x0%+15
n%=v%>>3
WHILE n%>0:A%=x%:B%=y%:C%=&F0:CALL plot%:x%-=1:n%-=1:ENDWHILE
A%=x%:B%=y%:C%=&E8+(v% AND 7):CALL plot%:x%-=1
WHILE x%>=x0%:A%=x%:B%=y%:C%=&E8:CALL plot%:x%-=1:ENDWHILE
ENDPROC

REM the statistics under the map (S, or W with the keys of 1983); each line
REM is drawn again only when what it shows changes
DEF PROCstats(all%)
LOCAL v%,k%,x%,t$
IF all% THEN SL1%=-1:SL2%=-1:SL3%=-1:SL4%=-1:SL5%=-1:SL6%=-1:SL7%=-1
IF NOT STS% THEN
  IF all% THEN FOR k%=24 TO 27:PROCtx(-4,k%,STRING$(40," ")):NEXT
  ENDPROC
ENDIF
IF all% THEN
  PROCtx(-3,24,"Magic"):PROCtx(-3,25,"Wounds"):PROCtx(-3,26,"Arrows")
  PROCtx(22,24,"Rings"):PROCtx(22,25,"Bag"):PROCtx(22,26,"Killed")
ENDIF
IF (MG%>>1)<>SL1% THEN SL1%=MG%>>1:PROCbarx(4,24,SL1%)
IF ws%!84<>SL2% THEN SL2%=ws%!84:PROCbarx(4,25,SL2%)
IF (AR%>>1)<>SL3% THEN SL3%=AR%>>1:PROCbarx(4,26,SL3%)
v%=0:FOR k%=0 TO 7:IF RF%(k%)=255 THEN v%+=1<<k%
NEXT
IF v%<>SL4% THEN
  SL4%=v%:x%=28
  FOR k%=7 TO 0 STEP -1
    IF RF%(k%)=255 THEN A%=x%:B%=24:C%=&88+k%:CALL plot%:x%+=1
  NEXT
  WHILE x%<36:A%=x%:B%=24:C%=32:CALL plot%:x%+=1:ENDWHILE
ENDIF
v%=TC%:FOR k%=1 TO TC%:v%=v%*7+SB%(k%):NEXT
IF v%<>SL5% THEN
  SL5%=v%
  FOR k%=1 TO 5
    IF k%<=TC% THEN C%=SB%(k%) ELSE C%=32
    A%=27+k%:B%=25:CALL plot%
  NEXT
ENDIF
IF ws%!88<>SL6% THEN SL6%=ws%!88:PROCtx(29,26,RIGHT$("  "+STR$SL6%,3))
IF (CF% AND 7)<>SL7% THEN
  SL7%=CF% AND 7
  IF CF%=8 THEN t$="Floor 0 - you have the golden key" ELSE t$="Floor "+STR$SL7%
  PROCtx(-3,27,LEFT$(t$+STRING$(39," "),39))
ENDIF
ENDPROC

REM ================= front end =================
DEF PROCfront
LOCAL k%
REPEAT
  CLS
  PROCtx(0,0,"Halls of the Things")
  IF KS% THEN
    PROCtx(0,2,"MOVEMENT  Cursor keys")
    PROCtx(2,4,"Doors   RETURN:Open/Close")
    PROCtx(0,6,"OBJECTS   E:Keep     Q:Drop")
    PROCtx(0,8,"WEAPONS   CTRL:Sword")
    PROCtx(2,10,"Arrows  SPACE:Shoot")
    PROCtx(10,11,"where you look")
    PROCtx(2,13,"Spells  F:Fireball L:Lightning")
    PROCtx(10,14,"H:Heal")
    PROCtx(0,16,"VARIOUS   TAB:Display status")
    PROCtx(10,17,"C:Centre  sh-C:Follow")
    PROCtx(10,18,"ESCAPE:Give up")
    PROCtx(10,19,"S:Statistics on/off")
    PROCtx(0,21,"M: the keys of 1983")
  ELSE
    PROCtx(0,2,"MOVEMENT  N:Up"):PROCtx(21,2,"M:Down")
    PROCtx(10,3,"Z:Left"):PROCtx(21,3,"X:Right")
    PROCtx(2,4,"Doors"):PROCtx(10,4,"O:Open"):PROCtx(21,4,"C:Close")
    PROCtx(0,6,"OBJECTS"):PROCtx(10,6,"K:Keep"):PROCtx(21,6,"D:Drop")
    PROCtx(0,8,"WEAPONS"):PROCtx(10,8,"S:Sword")
    PROCtx(2,10,"Arrows  A:Shoot")
    PROCtx(10,11,"5:Left"):PROCtx(21,11,"8:Right")
    PROCtx(10,12,"7:Up"):PROCtx(21,12,"6:Down")
    PROCtx(2,14,"Spells  F:Fireball L:Lightning")
    PROCtx(10,15,"H:Heal")
    PROCtx(0,17,"VARIOUS"):PROCtx(10,17,"1:Display status")
    PROCtx(9,18,"sp:Centre sh-sp:Follow")
    PROCtx(10,19,"W:Statistics on/off")
    PROCtx(0,21,"TAB: modern keys")
  ENDIF
  PROCtx(0,20,"High Score : "+HN$+"  "+STR$HS%)
  PROCtx(0,23,"Press SPACE to continue")
  REPEAT SYS "OS_Byte",19:UNTIL NOT INKEY(-99) AND NOT INKEY(-102) AND NOT INKEY(-97)
  REPEAT
    SYS "OS_Byte",19
    k%=0
    IF INKEY(-99) THEN k%=1
    IF KS% AND INKEY(-102) THEN k%=2
    IF KS%=0 AND INKEY(-97) THEN k%=2
  UNTIL k%
  IF k%=2 THEN KS%=1-KS%
UNTIL k%=1
SYS "OS_Byte",15,1
ENDPROC

DEF PROCwaitspace
REPEAT SYS "OS_Byte",19:UNTIL NOT INKEY(-99)
REPEAT SYS "OS_Byte",19:UNTIL INKEY(-99)
ENDPROC

REM FrontScore: how it ended, the high score
DEF PROCresult
LOCAL sc%,y%,t$,k%,n$
ND%=ws%!88
sc%=ND%*2+NR%*128
IF NR%=8 THEN sc%+=128
IF ET%=1 THEN A%=0:B%=1:sc%+=USR(scan%)
CLS
CASE ET% OF
WHEN 0:t$="You have been killed"
WHEN 1:t$="You are victorious !"
OTHERWISE:t$="You gave up , coward"
ENDCASE
PROCtx(0,0,t$)
CASE NR% OF
WHEN 0:t$="no rings"
WHEN 1:t$="one ring"
WHEN 7,8:t$="all seven rings"
OTHERWISE:t$=STR$NR%+" rings"
ENDCASE
PROCtx(0,2,"You collected "+t$)
y%=3
IF NR%=8 THEN PROCtx(0,y%,"and the golden key."):y%+=1
CASE ND% OF
WHEN 0:t$="no monsters"
WHEN 1:t$="one monster"
OTHERWISE:t$=STR$ND%+" monsters"
ENDCASE
PROCtx(0,y%+1,"You killed "+t$)
IF sc%=0 THEN t$="no" ELSE t$=STR$sc%
PROCtx(0,y%+2,"and scored "+t$+" points.")
y%+=4
IF sc%>HS% THEN
  PROCtx(0,y%,"Well done !")
  PROCtx(0,y%+1,"you have the high score")
  PROCtx(0,y%+2,"and have bettered "+HN$)
  PROCtx(0,y%+3,"by "+STR$(sc%-HS%)+" points.")
  HS%=sc%
  PROCtx(0,y%+5,"Please enter your name")
  PROCtx(0,y%+6,"____________")
  *FX15,1
  n$=""
  REPEAT
    k%=GET
    IF (k%=127 OR k%=8) AND LEN(n$)>0 THEN n$=LEFT$(n$,LEN(n$)-1):PROCtx(LEN(n$),y%+6,"_")
    IF k%>=32 AND k%<127 AND LEN(n$)<12 THEN PROCtx(LEN(n$),y%+6,CHR$k%):n$+=CHR$k%
  UNTIL k%=13
  WHILE LEFT$(n$,1)=" "
    n$=MID$(n$,2)
  ENDWHILE
  IF n$="" THEN n$="someone"
  HN$=n$
ENDIF
PROCtx(0,23,"Press SPACE to continue")
PROCwaitspace
ENDPROC
