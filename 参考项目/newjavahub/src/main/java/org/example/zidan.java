package org.example;

import javax.swing.*;
import java.awt.*;

public class zidan extends JLabel implements Obstacle {
    private static final long DURATION_MILLIS = 5050;
    private int x1;
    private int y1;
    private int x2;
    private int y2;
    private int x;
    private int y;
    private boolean flag = true;
    private boolean hit;
    private final long createdAt;
    public boolean getFlag(){
        return flag;
    }

    public boolean markHit() {
        if (hit) {
            return false;
        }
        hit = true;
        return true;
    }
    public int getX2() {
        return x2;
    }

    public zidan(int x1, int x2, int y1, int y2) {
        this.x1 = x1;
        this.y1 = y1;
        this.x2 = x2;
        this.y2 = y2;
        this.x = x1;
        this.y = y1;
        this.createdAt = System.currentTimeMillis();
        setOpaque(true); // 设置为不透明
        setBounds(x1 - 10, y1 - 10, 20, 20); // 设置初始位置
        setBackground(Color.RED); // 设置背景颜色为红色
    }

    public boolean updatePosition(long currentTime) {
        long elapsed = currentTime - createdAt;
        if (elapsed >= DURATION_MILLIS) {
            flag = false;
            return false;
        }
        double ratio = Math.min(1.0, (double) elapsed / DURATION_MILLIS);
        x = (int) (x1 + ratio * (x2 - x1));
        y = (int) (y1 + ratio * (y2 - y1));
        setLocation(x - 10, y - 10);
        return true;
    }

    @Override
    protected void paintComponent(Graphics g) {
        super.paintComponent(g);
        Graphics2D g2d = (Graphics2D) g;
        g2d.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);
        g2d.setColor(Color.RED); // 假设子弹是红色的
        g2d.fillOval(0, 0, 20, 20); // 绘制一个20x20的红圈
    }

    @Override
    public boolean judgement(int x, int y) {
        // 计算当前对象的x和y与传入的x和y之间的欧几里得距离
        int dx = this.x - x;
        int dy = this.y - y;
        double distance = Math.sqrt(dx * dx + dy * dy);
        // 判断距离是否小于等于 40
        return distance <= 40;
    }

    public int getY2() {
        return y2;
    }
}
