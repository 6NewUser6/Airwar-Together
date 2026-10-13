package org.example;

import javax.imageio.ImageIO;
import javax.swing.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.io.IOException;
import java.net.URL;
import java.util.Random;

public class help extends JLabel implements Obstacle {
    int x;
    int y;
    user_plane plane;
    boolean flag = false;
    private BufferedImage image;
    private boolean active;
    private long visibleSince;
    private ImageIcon icon;

    public help(user_plane plane) {
        Random random = new Random();
        int width = 1700;
        int height = 1200;
        this.x = random.nextInt(width - 200);
        this.y = random.nextInt(height - 200);
        this.setBounds(x, y, 40, 40);
        try {
            // 加载贴图
            URL imageURL = getResourceURL("help.jpg");
            if (imageURL != null) {
                image = ImageIO.read(imageURL);
            } else {
                System.err.println("Image not found: help.jpg");
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
        this.plane = plane;
    }

    @Override
    public boolean judgement(int x, int y) {
        Rectangle planeBounds = new Rectangle(x, y, plane.getWidth(), plane.getHeight());
        Rectangle helpBounds = new Rectangle(this.x, this.y, 40, 40);
        return planeBounds.intersects(helpBounds);
    }

    public void setHelp() {
        Random random = new Random();
        int width = 1700;
        int height = 1200;
        this.x = random.nextInt(width - 200);
        this.y = random.nextInt(height - 200);
        this.setBounds(x, y, getWidth(), getHeight());
    }

    public void justDoIt() {
        if (image != null && icon == null) {
            Image scaledImage = image.getScaledInstance(40, 40, Image.SCALE_SMOOTH);
            icon = new ImageIcon(scaledImage);
        }
        setIcon(icon);
        flag = true;
        active = true;
        visibleSince = System.currentTimeMillis();
    }

    public boolean update(long currentTime) {
        if (!active) {
            return false;
        }
        if (flag && judgement(plane.getX(), plane.getY())) {
            setIcon(null);
            plane.help();
            flag = false;
            active = false;
            return true;
        } else if (currentTime - visibleSince >= 3100) {
            setIcon(null);
            flag = false;
            active = false;
        }
        return false;
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = help.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }
}
