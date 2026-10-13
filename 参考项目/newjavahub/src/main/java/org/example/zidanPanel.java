package org.example;

import javax.swing.*;
import java.awt.*;
import java.util.Iterator;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;

public class zidanPanel extends JPanel {
    private final List<zidan> zidanPoints = new ArrayList<>();
    private final user_plane plane;
    private final Main_play play;

    public zidanPanel(user_plane plane, Main_play play, User u) {
        this.plane = plane;
        this.play = play;
        setLayout(null);
        setOpaque(false);
    }

    @Override
    protected void paintComponent(Graphics g) {
        super.paintComponent(g);
        drawZidans(g);
    }

    private void initializeZidans() {
        Random random = new Random();
        int width = getWidth();
        int height = getHeight();
        int x1 = 0, y1 = 0, x2 = 0, y2 = 0, h1, h2;
        h1 = random.nextInt(4); // 上下左右
        if (h1 == 0) {
            x1 = random.nextInt(width);
        } else if (h1 == 1) {
            y1 = height;
            x1 = random.nextInt(width);
        } else if (h1 == 2) {
            y1 = random.nextInt(height);
        } else {
            y1 = random.nextInt(height);
            x1 = width;
        }

        do {
            h2 = random.nextInt(4);
        } while (h2 == h1);
        if (h2 == 0) {
            x2 = random.nextInt(width) + 100;
        } else if (h2 == 1) {
            y2 = height;
            x2 = random.nextInt(width) + 100;
        } else if (h2 == 2) {
            y2 = random.nextInt(height) + 100;
        } else {
            y2 = random.nextInt(height) + 100;
            x2 = width;
        }
        zidan zidan = new zidan(x1, x2, y1, y2);
        zidanPoints.add(zidan);
        add(zidan); // 将子弹添加到面板中
    }

    private void drawZidans(Graphics g) {
        // 不需要手动绘制子弹，因为子弹是 JLabel 对象，会自动绘制
    }

    public void setAddNewZidan() {
        initializeZidans();
        repaint();
    }

    public void stopAllTimers() {
        play.stopTimers();
        zidanPoints.clear();
        removeAll(); // 清除所有子弹
        revalidate();
        repaint();
    }

    public boolean checkZidans(long currentTime) {
        int userX = plane.getX() + plane.getWidth() / 2;
        int userY = plane.getY() + plane.getHeight() / 2;
        int hits = 0;
        for (zidan bullet : zidanPoints) {
            if (bullet.judgement(userX, userY) && bullet.markHit() && plane.takeHit(currentTime)) {
                hits++;
            }
        }
        if (hits > 0) {
            ReadyFrame.killPlayer.playOnce();
            play.updateLifeLabel();
            return false;
        }
        return true;
    }

    public void updateZidans(long currentTime) {
        Iterator<zidan> iterator = zidanPoints.iterator();
        boolean removed = false;
        while (iterator.hasNext()) {
            zidan bullet = iterator.next();
            if (!bullet.updatePosition(currentTime)) {
                iterator.remove();
                remove(bullet);
                removed = true;
            }
        }
        if (removed) {
            revalidate();
        }
    }
}
