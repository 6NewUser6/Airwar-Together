package org.example;

import javax.imageio.ImageIO;
import javax.swing.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.io.IOException;
import java.net.URL;
import java.util.Random;

public class gold extends JLabel implements Obstacle {
    int x;
    int y;
    user_plane plane;
    boolean flag = false;
    private BufferedImage image;
    private boolean active;
    private long visibleSince;
    private ImageIcon icon;
    private User user;
    private Main_play play;

    public gold(user_plane plane) {
        Random random = new Random();
        int width = 1700;
        int height = 1200;
        this.x = random.nextInt(width - 200);
        this.y = random.nextInt(height - 200);
        this.setBounds(x, y, 40, 40);
        try {
            // 加载贴图
            URL imageURL = getResourceURL("gold.png");
            if (imageURL != null) {
                image = ImageIO.read(imageURL);
            } else {
                System.err.println("Image not found: gold.png");
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
        this.plane = plane;
    }

    @Override
    public boolean judgement(int x, int y) {
        // 计算给定坐标与 help 对象中心的距离
        int dx = Math.abs(this.x - x);
        int dy = Math.abs(this.y - y);
        return dx <= 20 && dy <= 20;
    }

    public void setGold() {
        Random random = new Random();
        int width = 1700;
        int height = 1200;
        this.x = random.nextInt(width - 200);
        this.y = random.nextInt(height - 200);
        this.setBounds(x, y, getWidth(), getHeight());
    }

    public void justDoIt(User user, Main_play play) {
        if (image != null && icon == null) {
            Image scaledImage = image.getScaledInstance(40, 40, Image.SCALE_SMOOTH);
            icon = new ImageIcon(scaledImage);
        }
        setIcon(icon);
        flag = true;
        active = true;
        visibleSince = System.currentTimeMillis();
        this.user = user;
        this.play = play;
    }

    public void update(long currentTime) {
        if (!active) {
            return;
        }
        if (flag && judgement(plane.getX(), plane.getY())) {
            setIcon(null);
            user.gold();
            flag = false;
            active = false;
            play.updategoldLable();
        } else if (currentTime - visibleSince >= 3100) {
            setIcon(null);
            flag = false;
            active = false;
        }
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = gold.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }
}
