package dev.pbzcraft;

import dev.skycraft.client.SkyClient;
import dev.skycraft.link.SkyLink;
import dev.skycraft.link.Proto;
import java.lang.foreign.*;
import java.lang.invoke.MethodHandle;
import java.lang.invoke.VarHandle;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.locks.LockSupport;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.phys.Vec3;
import static java.lang.foreign.ValueLayout.*;

public final class NativeCollision {
    private static final VarHandle INT=JAVA_INT.varHandle();
    private static final MethodHandle OPEN,MAP,CLOSE;
    private static MemorySegment shared;
    private static long nextOpen;
    public static long queries,timeouts,rejected,totalNanos,maxNanos;
    static {
        var linker=Linker.nativeLinker();
        var kernel=SymbolLookup.libraryLookup("kernel32",Arena.global());
        OPEN=linker.downcallHandle(kernel.find("OpenFileMappingW").orElseThrow(),FunctionDescriptor.of(ADDRESS,JAVA_INT,JAVA_INT,ADDRESS));
        MAP=linker.downcallHandle(kernel.find("MapViewOfFile").orElseThrow(),FunctionDescriptor.of(ADDRESS,ADDRESS,JAVA_INT,JAVA_INT,JAVA_INT,JAVA_LONG));
        CLOSE=linker.downcallHandle(kernel.find("CloseHandle").orElseThrow(),FunctionDescriptor.of(JAVA_INT,ADDRESS));
    }
    private static boolean ready() {
        if(shared==null && System.currentTimeMillis()>=nextOpen) {
            nextOpen=System.currentTimeMillis()+500;
            try(var arena=Arena.ofConfined()) {
                var name=arena.allocateFrom("Local\\PBZCraft.Collision_v1",StandardCharsets.UTF_16LE);
                var handle=(MemorySegment)OPEN.invokeExact(0xF001F,0,name);
                if(handle.address()==0)return false;
                var view=(MemorySegment)MAP.invokeExact(handle,0xF001F,0,0,512L);
                int closed=(int)CLOSE.invokeExact(handle);
                if(view.address()!=0)shared=view.reinterpret(512);
            } catch(Throwable error) {throw new IllegalStateException("PBZ native collision mapping",error);}
        }
        return shared!=null && shared.get(JAVA_INT,0)==0x435A4250 && shared.get(JAVA_INT,4)==1
            && shared.get(JAVA_INT,132)==1 && SkyLink.tickCount()-shared.get(JAVA_LONG,16)<1000;
    }
    public static Vec3 collide(LocalPlayer player,Vec3 movement) {
        if(!SkyClient.sky().inGame() || SkyClient.sky().loading() || !ready())return Vec3.ZERO;
        var skyShared=SkyLink.segment();
        if(skyShared==null || skyShared.get(JAVA_INT,Proto.OFF_MC_STATE+Proto.MS_TELEPORT_ACK)!=SkyClient.sky().teleportSeq)return Vec3.ZERO;
        int previous=(int)INT.getAcquire(shared,8L);
        if(previous!=(int)INT.getAcquire(shared,12L))return Vec3.ZERO;
        double[] values={player.getX(),player.getY(),player.getZ(),movement.x,movement.y,movement.z,
            player.getBbWidth(),player.getBbHeight(),player.onGround()?1:0};
        for(int i=0;i<values.length;i++)shared.set(JAVA_DOUBLE,32+i*8L,values[i]);
        shared.set(JAVA_LONG,24,SkyLink.tickCount());
        int sequence=previous+1;
        long start=System.nanoTime();
        INT.setRelease(shared,8L,sequence);
        while((int)INT.getAcquire(shared,12L)!=sequence) {
            if(System.nanoTime()-start>250_000_000L) {timeouts++;return Vec3.ZERO;}
            LockSupport.parkNanos(100_000L);
        }
        long elapsed=System.nanoTime()-start;
        queries++;totalNanos+=elapsed;maxNanos=Math.max(maxNanos,elapsed);
        if(shared.get(JAVA_INT,128)!=1) {rejected++;return Vec3.ZERO;}
        var resolved=new Vec3(shared.get(JAVA_DOUBLE,104),shared.get(JAVA_DOUBLE,112),shared.get(JAVA_DOUBLE,120));
        if(movement.y>0.01)System.out.printf("[PBZCollision] jump requested=%.6f resolved=%.6f atY=%.6f%n",movement.y,resolved.y,player.getY());
        if(!Double.isFinite(resolved.x) || !Double.isFinite(resolved.y) || !Double.isFinite(resolved.z) || resolved.lengthSqr()>4) {
            rejected++;return Vec3.ZERO;
        }
        return resolved;
    }
}
