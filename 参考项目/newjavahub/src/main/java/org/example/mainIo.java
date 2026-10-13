package org.example;

import javax.swing.*;
import java.awt.event.ActionEvent;
import java.awt.event.ActionListener;

public class mainIo extends JFrame {

    private JTextField userText;
    private JPasswordField passwordText;

    public mainIo() {
        // 设置窗口标题
        setTitle("欢迎使用我们的系统");

        // 设置窗口大小
        setSize(350, 200);

        // 设置窗口关闭操作
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);

        // 创建面板
        JPanel panel = new JPanel();
        panel.setLayout(null);

        // 用户名标签和文本框
        JLabel userLabel = new JLabel("用户名:");
        userLabel.setBounds(10, 20, 80, 25);
        panel.add(userLabel);

        userText = new JTextField(20);
        userText.setBounds(100, 20, 165, 25);
        panel.add(userText);

        // 密码标签和密码框
        JLabel passwordLabel = new JLabel("密码:");
        passwordLabel.setBounds(10, 50, 80, 25);
        panel.add(passwordLabel);

        passwordText = new JPasswordField(20);
        passwordText.setBounds(100, 50, 165, 25);
        panel.add(passwordText);

        // 登录按钮
        JButton loginButton = new JButton("登录");
        loginButton.setBounds(10, 80, 80, 25);
        loginButton.addActionListener(new ActionListener() {
            public void actionPerformed(ActionEvent e) {
                String user = userText.getText();
                String password = new String(passwordText.getPassword());
                User u = User.login(user, password);
                if (u.getId() == 0) {
                    JOptionPane.showMessageDialog(null, "你的密码或账号有误");
                } else {
                    // 进入游戏
                    ((JFrame) panel.getTopLevelAncestor()).dispose();
                    SwingUtilities.invokeLater(() -> {
                        ReadyFrame.audioPlayer.playLoop();
                        ReadyFrame frame = new ReadyFrame(u);
                        frame.setVisible(true);
                    });
                }
            }
        });
        panel.add(loginButton);

        // 注册按钮
        JButton registButton = new JButton("注册");
        registButton.setBounds(100, 80, 80, 25);
        registButton.addActionListener(new ActionListener() {
            public void actionPerformed(ActionEvent e) {
                regist();
            }
        });
        panel.add(registButton);

        JButton changeButton = new JButton("修改密码");
        changeButton.setBounds(200, 80, 100, 25);
        changeButton.addActionListener(new ActionListener() {
            public void actionPerformed(ActionEvent e) {
                changePassword();
            }
        });
        panel.add(changeButton);
        add(panel);

        setLocationRelativeTo(null);

        setVisible(true);
    }

    private void changePassword() {
        JFrame changePasswordFrame = new JFrame("修改密码");
        changePasswordFrame.setSize(350, 250);
        changePasswordFrame.setDefaultCloseOperation(JFrame.DISPOSE_ON_CLOSE);

        JPanel changePasswordPanel = new JPanel();
        changePasswordFrame.add(changePasswordPanel);
        placeChangePasswordComponents(changePasswordPanel);
        changePasswordFrame.setLocationRelativeTo(null);

        changePasswordFrame.setVisible(true);
    }

    private void placeChangePasswordComponents(JPanel panel) {
        panel.setLayout(null);

        JLabel userLabel = new JLabel("用户名");
        userLabel.setBounds(10, 20, 80, 25);
        panel.add(userLabel);

        userText = new JTextField(20);
        userText.setBounds(100, 20, 165, 25);
        panel.add(userText);

        JLabel currentPasswordLabel = new JLabel("当前密码");
        currentPasswordLabel.setBounds(10, 50, 80, 25);
        panel.add(currentPasswordLabel);

        JPasswordField currentPasswordText = new JPasswordField(20);
        currentPasswordText.setBounds(100, 50, 165, 25);
        panel.add(currentPasswordText);

        JLabel newPasswordLabel = new JLabel("新密码");
        newPasswordLabel.setBounds(10, 80, 80, 25);
        panel.add(newPasswordLabel);

        JPasswordField newPasswordText = new JPasswordField(20);
        newPasswordText.setBounds(100, 80, 165, 25);
        panel.add(newPasswordText);

        JLabel confirmNewPasswordLabel = new JLabel("确认新密码");
        confirmNewPasswordLabel.setBounds(10, 110, 100, 25);
        panel.add(confirmNewPasswordLabel);

        JPasswordField confirmNewPasswordText = new JPasswordField(20);
        confirmNewPasswordText.setBounds(110, 110, 155, 25);
        panel.add(confirmNewPasswordText);

        JButton changePasswordButton = new JButton("修改密码");
        changePasswordButton.setBounds(10, 140, 100, 25);
        changePasswordButton.addActionListener(new ActionListener() {
            public void actionPerformed(ActionEvent e) {
                String user = userText.getText();
                String currentPassword = new String(currentPasswordText.getPassword());
                String newPassword = new String(newPasswordText.getPassword());
                String confirmNewPassword = new String(confirmNewPasswordText.getPassword());

                if (!newPassword.equals(confirmNewPassword)) {
                    JOptionPane.showMessageDialog(null, "两次输入的新密码不一致");
                } else {
                    if (User.changePassword(user, currentPassword, newPassword)) {
                        JOptionPane.showMessageDialog(null, "密码修改成功");
                        ((JFrame) panel.getTopLevelAncestor()).dispose();
                    } else {
                        JOptionPane.showMessageDialog(null, "密码修改失败，请检查用户名和当前密码");
                    }
                }
            }
        });
        panel.add(changePasswordButton);
    }

    // 登录页面
    public static void regist() {
        JFrame registerFrame = new JFrame("Registration");
        registerFrame.setSize(350, 250);
        registerFrame.setDefaultCloseOperation(JFrame.DISPOSE_ON_CLOSE);

        JPanel registerPanel = new JPanel();
        registerFrame.add(registerPanel);
        placeRegisterComponents(registerPanel);
        registerFrame.setLocationRelativeTo(null);
        registerFrame.setVisible(true);
    }

    // 注册页面
    private static void placeRegisterComponents(JPanel panel) {
        panel.setLayout(null);

        JLabel userLabel = new JLabel("用户名");
        userLabel.setBounds(10, 20, 80, 25);
        panel.add(userLabel);

        JTextField userText = new JTextField(20);
        userText.setBounds(120, 20, 145, 25);
        panel.add(userText);

        JLabel passwordLabel = new JLabel("请输入你的密码");
        passwordLabel.setBounds(10, 50, 120, 25);
        panel.add(passwordLabel);

        JPasswordField passwordText = new JPasswordField(20);
        passwordText.setBounds(120, 50, 145, 25);
        panel.add(passwordText);

        JLabel confirmPasswordLabel = new JLabel("请再次输入你的密码");
        confirmPasswordLabel.setBounds(10, 80, 120, 25);
        panel.add(confirmPasswordLabel);

        JPasswordField confirmPasswordText = new JPasswordField(20);
        confirmPasswordText.setBounds(120, 80, 145, 25);
        panel.add(confirmPasswordText);

        JButton registButton = new JButton("注册");
        registButton.setBounds(10, 110, 80, 25);
        registButton.addActionListener(new ActionListener() {
            public void actionPerformed(ActionEvent e) {
                String user = userText.getText();
                String password = new String(passwordText.getPassword());
                String confirmPassword = new String(confirmPasswordText.getPassword());

                if (!password.equals(confirmPassword)) {
                    JOptionPane.showMessageDialog(null, "两次输入的密码不一致");
                } else {
                    if (User.registration(user, password)) {
                        JOptionPane.showMessageDialog(null, "注册成功，你现在可以登录了");
                        ((JFrame) panel.getTopLevelAncestor()).dispose();
                    } else {
                        JOptionPane.showMessageDialog(null, "注册失败，可能你的账号和他人重复了");
                    }
                }
            }
        });
        panel.add(registButton);
    }

    public static void main(String[] args) {
        // 创建并显示主页面
        new mainIo();
    }
}