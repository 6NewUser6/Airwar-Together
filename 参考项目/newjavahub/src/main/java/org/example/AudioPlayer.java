package org.example;

import javax.sound.sampled.*;
import java.io.IOException;
import java.net.URL;

public class AudioPlayer {

    private Clip clip;
    private FloatControl gainControl;

    public AudioPlayer(URL filePath) {
        if (filePath == null) {
            System.err.println("Audio file path is null.");
            return;
        }

        try {
            AudioInputStream audioStream = AudioSystem.getAudioInputStream(filePath);
            AudioFormat format = audioStream.getFormat();
            DataLine.Info info = new DataLine.Info(Clip.class, format);
            clip = (Clip) AudioSystem.getLine(info);
            clip.open(audioStream);

            // 获取音量控制器
            gainControl = (FloatControl) clip.getControl(FloatControl.Type.MASTER_GAIN);
        } catch (UnsupportedAudioFileException | IOException | LineUnavailableException e) {
            e.printStackTrace();
        }
    }

    public void playOnce() {
        if (clip != null) {
            new Thread(() -> {
                clip.setFramePosition(0); // 重置音频到开始位置
                clip.start();
                clip.drain(); // 阻塞直到当前音频数据播放完毕
            }).start();
        }
    }

    public void playLoop() {
        if (clip != null) {
            clip.setFramePosition(0); // 重置音频到开始位置
            clip.loop(Clip.LOOP_CONTINUOUSLY); // 循环播放
            clip.start();
        }
    }

    public void setVolume(float volume) {
        if (gainControl != null) {
            float min = gainControl.getMinimum();
            float max = gainControl.getMaximum();
            gainControl.setValue(min + (max - min) * volume);
        }
    }
}