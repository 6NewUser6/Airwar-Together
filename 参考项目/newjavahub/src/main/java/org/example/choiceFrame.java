package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.net.URL;

public class choiceFrame extends JFrame {
    private String plane;

    public choiceFrame(User user) {
        setTitle("选择飞机");
        setSize(650, 600);
        setDefaultCloseOperation(JFrame.DO_NOTHING_ON_CLOSE);
        addWindowListener(new WindowAdapter() {
            @Override
            public void windowClosing(WindowEvent e) {
                boolean flag = false;
                for (Component component : getContentPane().getComponents()) {
                    if (component instanceof JRadioButton) {
                        JRadioButton radioButton = (JRadioButton) component;
                        if (radioButton.isSelected()) {
                            plane = radioButton.getActionCommand();
                            user.setPlane(plane);
                            dispose();
                            SwingUtilities.invokeLater(() -> {
                                ReadyFrame readyFrame = new ReadyFrame(user);
                                readyFrame.setVisible(true);
                            });
                        }
                    }
                }
            }
        });
        setResizable(false);
        setLocationRelativeTo(null);
        setLayout(null);

        // 定义图像和按钮的初始位置
        int x = 50;
        int y = 50;
        int imageSize = 100; // 图像显示大小
        int gap = 40; // 图像和按钮之间的间隔
        int radioButtonSize = 30; // 勾选框大小

        // 创建 ButtonGroup 以确保只能选择一个 JRadioButton
        ButtonGroup buttonGroup = new ButtonGroup();
        int i = 1;
        int rowCount = 0;
        int colCount = 0;

        // 添加三排四列的图像和按钮
        for (int row = 0; row < 3; row++) {
            for (int col = 0; col < 4; col++) {
                String imagePath = "plane" + (i) + ".png";
                i++;

                if (user.hasPurchased((row * 4) + col)) {
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
                    imageLabel.setBounds(x + colCount * (imageSize + gap), y + rowCount * (imageSize + gap), imageSize, imageSize);
                    add(imageLabel);

                    // 创建对应的 JRadioButton
                    JRadioButton radioButton = new JRadioButton();
                    radioButton.setBounds(x + colCount * (imageSize + gap) + (imageSize - radioButtonSize) / 2, y + rowCount * (imageSize + gap) + imageSize + 10, radioButtonSize, radioButtonSize);
                    radioButton.setActionCommand(imagePath);
                    buttonGroup.add(radioButton);
                    add(radioButton);
                    if (row == 0 && col == 0) {
                        radioButton.setSelected(true);
                        plane = imagePath;
                        user.setPlane(plane);
                    }
                    colCount++;
                    if (colCount == 4) {
                        colCount = 0;
                        rowCount++;
                    }
                }
            }
        }

        JButton saveButton = new JButton("保存");
        saveButton.setBounds(400, 500, 80, 30); // 设置按钮位置和大小
        saveButton.addActionListener(e -> {
            boolean flag = false;
            // 获取选中的 JRadioButton 的 ActionCommand
            for (Component component : getContentPane().getComponents()) {
                if (component instanceof JRadioButton) {
                    JRadioButton radioButton = (JRadioButton) component;
                    if (radioButton.isSelected()) {
                        plane = radioButton.getActionCommand();
                        user.setPlane(plane);
                        dispose();
                        SwingUtilities.invokeLater(() -> {
                            ReadyFrame readyFrame = new ReadyFrame(user);
                            readyFrame.setVisible(true);
                        });
                    }
                }
            }
        });
        add(saveButton);
        setVisible(true);
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = choiceFrame.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }

    public static void main(String[] args) {
        // 创建一个虚拟的 User 对象
        User user = new User();
        // 创建并显示购物窗口
        new choiceFrame(user);
    }
}