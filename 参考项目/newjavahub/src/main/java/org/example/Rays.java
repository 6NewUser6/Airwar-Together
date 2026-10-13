package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.geom.Line2D;
import java.util.Objects;

public class Rays implements Obstacle {
    private int x1;
    private int y1;
    private int x2;
    private int y2;
    private Color color;
    private final long createdAt;
    private boolean terminalBlackFrame;
    private boolean hit;

    public int getX1() {
        return x1;
    }

    public int getY1() {
        return y1;
    }

    public void setX1(int x1) {
        this.x1 = x1;
    }

    public void setY1(int y1) {
        this.y1 = y1;
    }

    public int getX2() {
        return x2;
    }

    public void setX2(int x2) {
        this.x2 = x2;
    }

    public int getY2() {
        return y2;
    }

    public void setY2(int y2) {
        this.y2 = y2;
    }

    public Color getColor() {
        return color;
    }

    public void setColor(Color color) {
        this.color = color;
    }

    public Rays(int x1, int x2, int y1, int y2) {
        this.x1 = x1;
        this.y1 = y1;
        this.x2 = x2;
        this.y2 = y2;
        this.color = Color.RED; // 默认颜色为红色
        this.createdAt = System.currentTimeMillis();
    }

    public boolean update(long currentTime) {
        long age = currentTime - createdAt;
        Color nextColor;
        if (age < 600) {
            nextColor = Color.RED;
        } else if (age < 800) {
            nextColor = new Color(255, 0, 0, 0);
        } else if (age < 1200 || !terminalBlackFrame) {
            nextColor = Color.BLACK;
            if (age >= 1200) {
                terminalBlackFrame = true;
            }
        } else {
            nextColor = null;
        }
        boolean changed = !Objects.equals(color, nextColor);
        color = nextColor;
        return changed;
    }

    @Override
    public boolean judgement(int x, int y) {
        return Line2D.ptSegDist(x1, y1, x2, y2, x, y) <= 20;
    }

    public boolean intersects(Rectangle previousBounds, Rectangle currentBounds) {
        double previousX = previousBounds.getCenterX();
        double previousY = previousBounds.getCenterY();
        double currentX = currentBounds.getCenterX();
        double currentY = currentBounds.getCenterY();
        if (Line2D.linesIntersect(x1, y1, x2, y2, previousX, previousY, currentX, currentY)) {
            return true;
        }
        double distance = Math.min(
                Math.min(Line2D.ptSegDist(x1, y1, x2, y2, previousX, previousY),
                        Line2D.ptSegDist(x1, y1, x2, y2, currentX, currentY)),
                Math.min(Line2D.ptSegDist(previousX, previousY, currentX, currentY, x1, y1),
                        Line2D.ptSegDist(previousX, previousY, currentX, currentY, x2, y2)));
        double radius = Math.hypot(currentBounds.width, currentBounds.height) / 2.0 + 4;
        return distance <= radius;
    }

    public boolean markHit() {
        if (hit) {
            return false;
        }
        hit = true;
        return true;
    }

    public boolean isBlack() {
        return Color.BLACK.equals(color);
    }

}
