package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.MouseAdapter;
import java.awt.event.MouseEvent;
import java.awt.event.WindowAdapter;
import java.awt.event.WindowEvent;
import java.net.URL;

import static java.lang.System.exit;

public class ReadyFrame extends JFrame {
    public String plane;
    private final User user;
    public static final AudioPlayer killPlayer = new AudioPlayer(getResourceURL("out.wav"));
    public static final AudioPlayer audioPlayer = new AudioPlayer(getResourceURL("backgroundMusic.wav"));

    public ReadyFrame(User user) {
        this.user = user;
        plane = user.getPlane();
        setTitle("一起打飞机");
        setSize(600, 400);
        setDefaultCloseOperation(JFrame.DISPOSE_ON_CLOSE);
        setResizable(false);
        setLocationRelativeTo(null);
        setLayout(null);
        JPanel contentPane = getJPanelBackground();
        setContentPane(contentPane);

        JLabel usernameLabel = new JLabel("欢迎" + user.getName());
        usernameLabel.setForeground(Color.red);
        usernameLabel.setBounds(10, 10, 150, 20);
        contentPane.add(usernameLabel);

        // 创建标题标签
        Dimension titleSize = new Dimension(300, 100);
        JLabel titleLabel = createClickableLabel(getResourceURL("title.png"), titleSize);
        titleLabel.setBounds(150, 30, titleSize.width, titleSize.height);
        contentPane.add(titleLabel);

        // 创建面板用于按钮
        JPanel buttonPanel = new JPanel();
        buttonPanel.setOpaque(false);
        buttonPanel.setLayout(new BoxLayout(buttonPanel, BoxLayout.Y_AXIS));
        buttonPanel.setBounds(225, 150, 200, 200);
        contentPane.add(buttonPanel);

        Dimension buttonSize = new Dimension(150, 40);
        JLabel startGameLabel = createClickableLabel(getResourceURL("startGame.png"), buttonSize);
        JLabel choiceLabel = createClickableLabel(getResourceURL("choice.png"), buttonSize);
        JLabel shoppingLabel = createClickableLabel(getResourceURL("shopping.png"), buttonSize);
        JLabel difficultyLabel = createClickableLabel(getResourceURL("difficult.png"), buttonSize);

        buttonPanel.add(startGameLabel);
        buttonPanel.add(Box.createVerticalStrut(10));
        buttonPanel.add(choiceLabel);
        buttonPanel.add(Box.createVerticalStrut(10));
        buttonPanel.add(shoppingLabel);
        buttonPanel.add(Box.createVerticalStrut(10));
        buttonPanel.add(difficultyLabel);

        choiceLabel.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                dispose();
                SwingUtilities.invokeLater(() -> {
                    choiceFrame choiceFrame = new choiceFrame(user);
                    choiceFrame.setVisible(true);
                });
            }
        });

        startGameLabel.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                dispose();
                // 打开新的 Main_play 窗口
                SwingUtilities.invokeLater(() -> {
                    Main_play mainPlayFrame = new Main_play(user, plane);
                    mainPlayFrame.setVisible(true);
                });
            }
        });

        shoppingLabel.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                dispose();
                // 打开新的 Main_play 窗口
                SwingUtilities.invokeLater(() -> {
                    shoppingFrame shoppingFrame = new shoppingFrame(user);
                    shoppingFrame.setVisible(true);
                });
            }
        });

        difficultyLabel.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                SwingUtilities.invokeLater(() -> {
                    GameChoiceFrame gameChoiceFrame = new GameChoiceFrame();
                    gameChoiceFrame.setVisible(true);
                });
            }
        });

        Dimension settingSize = new Dimension(80, 40);
        JLabel settingLabel = createClickableLabel(getResourceURL("setting.png"), settingSize);
        settingLabel.setBounds(getWidth() - settingSize.width - 30, 10, settingSize.width, settingSize.height);
        settingLabel.addMouseListener(new MouseAdapter() {
            @Override
            public void mouseClicked(MouseEvent e) {
                SwingUtilities.invokeLater(() -> {
                    SettingFrame settingFrame = new SettingFrame();
                    settingFrame.setVisible(true);
                });
            }
        });
        contentPane.add(settingLabel);

        // 添加窗口关闭监听器
        addWindowListener(new WindowAdapter() {
            @Override
            public void windowClosing(WindowEvent e) {
                onWindowClosing();
            }
        });
        setVisible(true);
    }

    private void onWindowClosing() {
        user.save(user.getPassword());
        exit(0);
    }

    private static JPanel getJPanelBackground() {
        JPanel contentPane = new JPanel() {
            @Override
            protected void paintComponent(Graphics g) {
                super.paintComponent(g);
                g.setColor(Color.LIGHT_GRAY);
                g.fillRect(0, 0, getWidth(), getHeight());
                URL backgroundImageURL = getResourceURL("background.png");
                if (backgroundImageURL != null) {
                    ImageIcon backgroundImage = new ImageIcon(backgroundImageURL);
                    g.drawImage(backgroundImage.getImage(), 0, 0, getWidth(), getHeight(), null);
                } else {
                    System.err.println("Background image not found.");
                }
            }
        };

        // 创建内容面板
        contentPane.setLayout(null); // 禁用布局管理器
        return contentPane;
    }

    private JLabel createClickableLabel(URL imageURL, Dimension size) {
        if (imageURL == null) {
            System.err.println("Image URL is null.");
            return new JLabel();
        }

        ImageIcon icon = new ImageIcon(imageURL);
        JLabel label = new JLabel(icon) {
            @Override
            protected void paintComponent(Graphics g) {
                g.drawImage(icon.getImage(), 0, 0, getWidth(), getHeight(), this);
            }
        };
        label.setPreferredSize(size);
        label.setMaximumSize(size);
        label.setMinimumSize(size);
        return label;
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = ReadyFrame.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }
}