package org.example;

import javax.imageio.ImageIO;
import javax.swing.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.io.IOException;
import java.net.URL;

public class user_plane extends JLabel {
    private static final long INVULNERABILITY_MILLIS = 2000;
    private static final long BLINK_INTERVAL_MILLIS = 100;
    private BufferedImage image;
    private int lives = 5;
    private long invulnerableUntil;
    private long invulnerableSince;

    public int getLives() {
        return lives;
    }

    public user_plane(String file) {
        try {
            URL imageURL = getResourceURL(file);
            if (imageURL != null) {
                image = ImageIO.read(imageURL);
            } else {
                System.err.println("Image not found: " + file);
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    @Override
    protected void paintComponent(Graphics g) {
        super.paintComponent(g);
        if (image != null) {
            // 绘制贴图
            g.drawImage(image, 0, 0, getWidth(), getHeight(), this);
        } else {
            // 如果贴图加载失败，使用红色填充
            g.setColor(Color.RED);
            g.fillOval(0, 0, getWidth(), getHeight());
        }
    }

    public void help() {
        lives += 1;
    }

    public boolean takeHit(long currentTime) {
        if (currentTime < invulnerableUntil) {
            return false;
        }
        lives--;
        invulnerableSince = currentTime;
        invulnerableUntil = currentTime + INVULNERABILITY_MILLIS;
        setVisible(true);
        return true;
    }

    public void updateInvulnerabilityVisual(long currentTime) {
        boolean visible = currentTime >= invulnerableUntil
                || (currentTime - invulnerableSince) / BLINK_INTERVAL_MILLIS % 2 == 0;
        if (isVisible() != visible) {
            setVisible(visible);
        }
    }

    public void setLives(int life) {
        lives = life;
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = user_plane.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }
}
