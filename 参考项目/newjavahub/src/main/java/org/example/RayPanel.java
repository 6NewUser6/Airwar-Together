package org.example;

import javax.swing.*;
import java.awt.*;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Random;

public class RayPanel extends JPanel {
    private final List<Rays> rayPoints = new ArrayList<>();
    private final user_plane plane;
    private final Main_play play;
    private int score = 0;
    private int magnification = 10;
    private final User user;
    public RayPanel(user_plane plane, Main_play play, User u) {
        this.plane = plane;
        this.play = play;
        this.user = u;
        setOpaque(false);
    }
    @Override
    protected void paintComponent(Graphics g) {
        super.paintComponent(g);
        drawRays(g);
    }

    private void initializeRays() {
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
            x2 = random.nextInt(width);
        } else if (h2 == 1) {
            y2 = height;
            x2 = random.nextInt(width);
        } else if (h2 == 2) {
            y2 = random.nextInt(height);
        } else {
            y2 = random.nextInt(height);
            x2 = width;
        }
        rayPoints.add(new Rays(x1, x2, y1, y2));
    }

    public boolean checkCollisions(Rectangle previousPlaneBounds, Rectangle currentPlaneBounds, long currentTime) {
        int hits = 0;
        for (Rays ray : rayPoints) {
            if (ray.isBlack() && ray.intersects(previousPlaneBounds, currentPlaneBounds)
                    && ray.markHit() && plane.takeHit(currentTime)) {
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

    public void advanceScore() {
        score += magnification;
        play.updateScoreLabel(score);
    }

    public void died() {
        if (plane.getLives() <= 0) {
            stopAllTimers(); // 停止所有计时器
            play.dispose(); // 关闭当前窗口
            new ReadyFrame(user).setVisible(true); // 打开新的 ReadyFrame
            rayPoints.clear();
        }
    }

    private void drawRays(Graphics g) {
        Graphics2D g2d = (Graphics2D) g;
        g2d.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);

        for (Rays ray : rayPoints) {
            if (ray.getColor() == null) {
                continue;
            }
            g2d.setColor(ray.getColor());
            g2d.drawLine(ray.getX1(), ray.getY1(), ray.getX2(), ray.getY2());
        }
    }

    public void setAddNewRay() {
        initializeRays();
        repaint();
    }

    public boolean updateRays(long currentTime) {
        boolean changed = false;
        Iterator<Rays> iterator = rayPoints.iterator();
        while (iterator.hasNext()) {
            Rays ray = iterator.next();
            changed |= ray.update(currentTime);
            if (ray.getColor() == null) {
                iterator.remove();
                changed = true;
            }
        }
        return changed;
    }

    public void findPlane() {
        Random random = new Random();
        int width = getWidth();
        int height = getHeight();

        int x1 = 0, y1 = 0, x2, y2, h1;
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
        int userX = plane.getX() + plane.getWidth() / 2;
        int userY = plane.getY() + plane.getHeight() / 2;
        // 计算射线方向向量
        double dx = userX - x1;
        double dy = userY - y1;
        double length = Math.sqrt(dx * dx + dy * dy);
        dx /= length;
        dy /= length;
        // 计算射线终点
        double t = 10000;
        x2 = (int) (x1 + dx * t);
        y2 = (int) (y1 + dy * t);
        rayPoints.add(new Rays(x1, x2, y1, y2));
        repaint();
    }
    private void stopAllTimers() {
        play.stopTimers();
        rayPoints.clear();
    }
}
