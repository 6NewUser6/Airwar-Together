package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;

public class SettingFrame extends JFrame {

    private JCheckBox musicCheckBox;
    private JCheckBox soundEffectCheckBox;
    private JButton saveButton;

    public SettingFrame() {
        setTitle("设置");
        setSize(300, 150);

        setDefaultCloseOperation(JFrame.DISPOSE_ON_CLOSE);


        JPanel panel = new JPanel();
        panel.setLayout(new GridBagLayout());
        GridBagConstraints gbc = new GridBagConstraints();
        gbc.insets = new Insets(5, 5, 5, 5); // 设置边距


        musicCheckBox = new JCheckBox("音乐");
        musicCheckBox.setSelected(true); // 默认勾选
        gbc.gridx = 0;
        gbc.gridy = 0;
        panel.add(musicCheckBox, gbc);

        soundEffectCheckBox = new JCheckBox("音效");
        soundEffectCheckBox.setSelected(true); // 默认勾选
        gbc.gridx = 0;
        gbc.gridy = 1;
        panel.add(soundEffectCheckBox, gbc);

        saveButton = new JButton("保存");
        saveButton.addActionListener(new ActionListener() {
            @Override
            public void actionPerformed(ActionEvent e) {
                saveSettings();
            }
        });
        gbc.gridx = 0;
        gbc.gridy = 2;
        gbc.gridwidth = 1;
        gbc.anchor = GridBagConstraints.CENTER; // 按钮居中
        panel.add(saveButton, gbc);
        add(panel);
        setLocationRelativeTo(null);
        setVisible(true);
    }

    private void saveSettings() {

        boolean musicEnabled = musicCheckBox.isSelected();
        boolean soundEffectEnabled = soundEffectCheckBox.isSelected();

        if (musicEnabled) {
            ReadyFrame.audioPlayer.setVolume(1);
        }else {
            ReadyFrame.audioPlayer.setVolume(0);
        }
        if (soundEffectEnabled) {
            ReadyFrame.killPlayer.setVolume(1);
        }else {
            ReadyFrame.killPlayer.setVolume(0);
        }
        dispose();

    }

    public static void main(String[] args) {
        new SettingFrame();
    }
}