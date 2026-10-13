package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.net.URL;

public class shoppingFrame extends JFrame {
    public shoppingFrame(User user) {

        setTitle("快乐商城");
        setSize(650, 600);
        setDefaultCloseOperation(JFrame.DO_NOTHING_ON_CLOSE);
        addWindowListener(new WindowAdapter() {
            @Override
            public void windowClosing(WindowEvent e) {
                dispose();
                ReadyFrame readyFrame = new ReadyFrame(user);
                readyFrame.setVisible(true);
            }
        });
        setResizable(false);
        setLocationRelativeTo(null);
        setLayout(null);

        int x = 50;
        int y = 50;
        int imageSize = 100; // 图像显示大小
        int gap = 40; // 图像和按钮之间的间隔
        int labelWidth = 70; // 调整后的勾选框宽度
        int labelHeight = 20; // 调整后的勾选框高度

        int i = 1;
        // 添加三排四列的图像和按钮
        JLabel goldLabel = new JLabel("金币: " + user.getGold());
        goldLabel.setBounds(450, 30, 200, 20);
        goldLabel.setFont(new Font("宋体", Font.PLAIN, 16));
        add(goldLabel);

        for (int row = 0; row < 3; row++) {
            for (int col = 0; col < 4; col++) {
                String imagePath = "plane" + (i) + ".png";
                i++;
                URL imageURL = getResourceURL(imagePath);
                if (imageURL == null) {
                    System.err.println("Image not found: " + imagePath);
                    continue;
                }
                ImageIcon originalIcon = new ImageIcon(imageURL);
                Image scaledImage = originalIcon.getImage().getScaledInstance(imageSize, imageSize, Image.SCALE_SMOOTH);
                ImageIcon scaledIcon = new ImageIcon(scaledImage);

                // 创建 JLabel 并设置缩放后的图像
                JLabel imageLabel = new JLabel(scaledIcon);
                imageLabel.setBounds(x + col * (imageSize + gap), y + row * (imageSize + gap), imageSize, imageSize);
                add(imageLabel);

                // 创建对应的 JLabel 用于显示购买状态
                JLabel buyLabel = new JLabel();
                buyLabel.setBounds(x + col * (imageSize + gap) + (imageSize - labelWidth) / 2, y + row * (imageSize + gap) + imageSize + 10, labelWidth, labelHeight);
                int planeIndex = (row * 4) + col;
                if (user.hasPurchased(planeIndex)) {
                    buyLabel.setIcon(createScaledIcon(getResourceURL("get.png"), labelWidth, labelHeight));
                } else {
                    buyLabel.setIcon(createScaledIcon(getResourceURL("buy.png"), labelWidth, labelHeight));
                    buyLabel.addMouseListener(new MouseAdapter() {
                        @Override
                        public void mouseClicked(MouseEvent e) {
                            if (user.getGold() >= 25) {
                                user.purchasePlane(planeIndex);
                                user.setGold(user.getGold() - 25);
                                buyLabel.setIcon(createScaledIcon(getResourceURL("get.png"), labelWidth, labelHeight));
                                goldLabel.setText("金币: " + user.getGold());
                            } else {
                                JOptionPane.showMessageDialog(shoppingFrame.this, "金币不足，无法购买飞机", "金币不足", JOptionPane.WARNING_MESSAGE);
                            }
                        }
                    });
                }
                add(buyLabel);
            }
        }

        JButton saveButton = new JButton("退出");
        saveButton.setBounds(400, 500, 80, 30);
        saveButton.addActionListener(e -> {
            dispose();
            SwingUtilities.invokeLater(() -> {
                ReadyFrame readyFrame = new ReadyFrame(user);
                readyFrame.setVisible(true);
            });
        });
        add(saveButton);
        setVisible(true);
    }

    private ImageIcon createScaledIcon(URL url, int width, int height) {
        if (url == null) {
            System.err.println("Image URL is null.");
            return new ImageIcon();
        }
        ImageIcon originalIcon = new ImageIcon(url);
        Image scaledImage = originalIcon.getImage().getScaledInstance(width, height, Image.SCALE_SMOOTH);
        return new ImageIcon(scaledImage);
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = shoppingFrame.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }

}