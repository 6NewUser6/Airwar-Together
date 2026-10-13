package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;

public class GameChoiceFrame extends JFrame {

    private ButtonGroup difficultyGroup;
    private JRadioButton easyRadioButton;
    private JRadioButton normalRadioButton;
    private JRadioButton hardRadioButton;
    private JRadioButton hellRadioButton;
    private JButton saveButton;

    public GameChoiceFrame() {
        // 设置窗口标题
        setTitle("选择难度");

        // 设置窗口大小
        setSize(300, 300);

        // 设置窗口关闭操作
        setDefaultCloseOperation(JFrame.DISPOSE_ON_CLOSE);

        // 创建主面板
        JPanel mainPanel = new JPanel();
        mainPanel.setLayout(new BorderLayout());

        // 创建内容面板
        JPanel contentPanel = new JPanel();
        contentPanel.setLayout(new BoxLayout(contentPanel, BoxLayout.Y_AXIS));
        contentPanel.setBorder(BorderFactory.createEmptyBorder(20, 20, 20, 20)); // 上下左右间距

        // 设置字体
        Font font = new Font("微软雅黑", Font.PLAIN, 16);

        // 创建单选按钮组
        difficultyGroup = new ButtonGroup();

        // 创建简单难度单选按钮
        easyRadioButton = new JRadioButton("简单");
        easyRadioButton.setFont(font);
        easyRadioButton.setAlignmentX(Component.CENTER_ALIGNMENT); // 居中对齐
        contentPanel.add(easyRadioButton);
        difficultyGroup.add(easyRadioButton);
        easyRadioButton.setSelected(true);

        // 创建普通难度单选按钮
        normalRadioButton = new JRadioButton("普通");
        normalRadioButton.setFont(font);
        normalRadioButton.setAlignmentX(Component.CENTER_ALIGNMENT); // 居中对齐
        contentPanel.add(normalRadioButton);
        difficultyGroup.add(normalRadioButton);

        // 创建困难难度单选按钮
        hardRadioButton = new JRadioButton("困难");
        hardRadioButton.setFont(font);
        hardRadioButton.setAlignmentX(Component.CENTER_ALIGNMENT); // 居中对齐
        contentPanel.add(hardRadioButton);
        difficultyGroup.add(hardRadioButton);

        // 创建地狱难度单选按钮
        hellRadioButton = new JRadioButton("地狱");
        hellRadioButton.setFont(font);
        hellRadioButton.setAlignmentX(Component.CENTER_ALIGNMENT); // 居中对齐
        contentPanel.add(hellRadioButton);
        difficultyGroup.add(hellRadioButton);

        // 创建保存按钮
        saveButton = new JButton("保存");
        saveButton.setFont(font);
        saveButton.setAlignmentX(Component.CENTER_ALIGNMENT); // 居中对齐
        saveButton.addActionListener(new ActionListener() {
            @Override
            public void actionPerformed(ActionEvent e) {
                saveSettings();
            }
        });
        contentPanel.add(Box.createVerticalStrut(20)); // 增加间距
        contentPanel.add(saveButton);

        // 将内容面板添加到主面板
        mainPanel.add(contentPanel, BorderLayout.CENTER);

        // 将主面板添加到窗口
        add(mainPanel);

        // 设置窗口在屏幕中间显示
        setLocationRelativeTo(null);

        // 设置窗口可见
        setVisible(true);
    }

    private void saveSettings() {

        if (easyRadioButton.isSelected()) {
            Main_play.a = 800;
        } else if (normalRadioButton.isSelected()) {
            Main_play.a = 600;
        } else if (hardRadioButton.isSelected()) {
            Main_play.a = 400;
        } else if (hellRadioButton.isSelected()) {
            Main_play.a = 300;
        }
        dispose();
    }

    public static void main(String[] args) {
        // 创建并显示选择难度窗口
        new GameChoiceFrame();
    }
}