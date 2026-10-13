package org.example;

import java.io.*;
import java.util.*;
public class TestPiped{
    public static void main(String [] args){
        sender s = new sender();
        receiver r = new receiver();
        PipedOutputStream out = s.getOut();
        PipedInputStream in = r.getIn();
        try{
            in.connect(out);
            s.start();
            r.start();
        }catch(Exception e){
            e.printStackTrace();
        }
    }
}
class sender extends Thread {
    PipedOutputStream out = new PipedOutputStream();
    public PipedOutputStream getOut(){
        return out;
    }
    public void run() {
        String str = "Hello,receiver ! I`m sender\n";
        try {
            out.write(str.getBytes());
            out.close();
        } catch(Exception e) {
            e.printStackTrace();
        }
    }
}
class receiver extends Thread {
    PipedInputStream in = new PipedInputStream();
    public PipedInputStream getIn() {
        return in;
    }
    public void run(){
        byte [] buf = new byte[1024];
        try {
            int len = in.read(buf);
            System.out.println("the following is from sender:\n"+new String(buf,0,len));
            in.close();
        }catch(Exception e) {
            e.printStackTrace();
        }
    }
}
