package org.example;

import javax.swing.*;
import java.awt.*;
import java.awt.event.*;
import java.awt.image.BufferedImage;
import java.net.URL;

public class Main_play extends JFrame {
    private final user_plane plane;
    private final RayPanel rayPanel;
    private final zidanPanel zidanPanel; // 新增 zidanPanel
    private final Timer timer;
    public static int a = 800;
    private final JLabel lifeLabel;
    private final JLabel scoreLabel;
    private final JLabel mygoldLabel;
    private final help helpLabel;
    private final gold goldLabel;
    private long lastAddNewRayTime = 0;
    private long lastFindPlaneTime = 0;
    private long lastScoreTime = 0;
    private long lastDamageTime = 0;
    private long helpTime = 0;
    private long goldTime = System.currentTimeMillis() + 1000;
    private int mouseX = 0;
    private int mouseY = 0;
    private final User user;
    public static int addTime;

    public Main_play(User user, String file) {

        // 初始化窗口
        this.user = user;
        setTitle("飞机？boom！");
        setSize(1700, 1200);
        setResizable(false);
        setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        setLocationRelativeTo(null);
        setLayout(null);

        // 创建一个 JLayeredPane 来管理组件的层次结构
        JLayeredPane layeredPane = new JLayeredPane();
        layeredPane.setPreferredSize(new Dimension(1700, 1200));
        setContentPane(layeredPane);

        // 创建一个自定义的 JLabel 并设置其大小为一个圆
        plane = new user_plane(file);
        plane.setBounds(0, 0, 50, 50); // 设置为与窗口同大小
        setCursor(getToolkit().createCustomCursor(
                new BufferedImage(1, 1, BufferedImage.TYPE_INT_ARGB),
                new Point(),
                "invisibleCursor"));
        if (a == 300) {
            plane.setLives(1);
        }
        // 创建并添加 rayPanel
        rayPanel = new RayPanel(plane, this, user);
        rayPanel.setBounds(0, 0, getWidth(), getHeight()); // 设置 rayPanel 的边界
        rayPanel.setVisible(true); // 确保 rayPanel 可见
        layeredPane.add(rayPanel, Integer.valueOf(1));

        // 创建并添加 zidanPanel
        zidanPanel = new zidanPanel(plane, this, user); // 新增 zidanPanel
        zidanPanel.setBounds(0, 0, getWidth(), getHeight()); // 设置 zidanPanel 的边界
        zidanPanel.setVisible(true); // 确保 zidanPanel 可见
        layeredPane.add(zidanPanel, Integer.valueOf(1)); // 将 zidanPanel 添加到 JLayeredPane

        // 将 trackingLabel 添加到 JFrame 的内容面板
        layeredPane.add(plane, Integer.valueOf(5));

        // 创建并添加生命值标签
        lifeLabel = new JLabel("生命:" + plane.getLives());
        lifeLabel.setBounds(10, 10, 100, 20); // 设置标签的位置和大小
        lifeLabel.setFont(new Font("宋体", Font.PLAIN, 14));
        layeredPane.add(lifeLabel, Integer.valueOf(2));

        scoreLabel = new JLabel("当前分数:0");
        scoreLabel.setBounds(1500, 10, 150, 20); // 设置标签的位置和大小
        scoreLabel.setFont(new Font("宋体", Font.PLAIN, 16)); // 设置字体
        layeredPane.add(scoreLabel, Integer.valueOf(2));

        JLabel word = new JLabel("可以按esc退出");
        word.setBounds(800, 10, 150, 20); // 设置标签的位置和大小
        word.setFont(new Font("宋体", Font.PLAIN, 16)); // 设置字体
        layeredPane.add(word, Integer.valueOf(2));

        mygoldLabel = new JLabel("当前金币:" + user.getGold());
        mygoldLabel.setBounds(1500, 50, 150, 20); // 设置标签的位置和大小
        mygoldLabel.setFont(new Font("宋体", Font.PLAIN, 16)); // 设置字体
        layeredPane.add(mygoldLabel, Integer.valueOf(2));

        URL backgroundImageURL = getResourceURL("mainBackground.jpg");
        if (backgroundImageURL != null) {
            ImageIcon backgroundImageIcon = new ImageIcon(backgroundImageURL);
            Image backgroundImage = backgroundImageIcon.getImage().getScaledInstance(getWidth(), getHeight(), Image.SCALE_SMOOTH);
            ImageIcon scaledBackgroundImageIcon = new ImageIcon(backgroundImage);
            JLabel backgroundLabel = new JLabel(scaledBackgroundImageIcon);
            backgroundLabel.setBounds(0, 0, getWidth(), getHeight());
            layeredPane.add(backgroundLabel, Integer.valueOf(0));
        } else {
            System.err.println("Background image not found: mainBackground.jpg");
        }

        // 设置窗口可见
        setVisible(true);
        helpLabel = new help(plane);
        layeredPane.add(helpLabel, Integer.valueOf(2));
        helpLabel.setVisible(false);
        goldLabel = new gold(plane);
        layeredPane.add(goldLabel, Integer.valueOf(2));
        goldLabel.setVisible(false);

        // 添加 ESC 键事件监听器
        addKeyListener(new KeyAdapter() {
            @Override
            public void keyPressed(KeyEvent e) {
                if (e.getKeyCode() == KeyEvent.VK_ESCAPE) {
                    onEscPressed();
                }
            }
        });
        // 初始化 Timer，每秒增加一条射线
        addTime = a;
        timer = new Timer(10, e -> {
            long currentTime = System.currentTimeMillis();
            plane.updateInvulnerabilityVisual(currentTime);
            updateMousePosition(layeredPane);
            Rectangle previousPlaneBounds = plane.getBounds();
            updatePlanePosition(mouseX, mouseY);
            Rectangle currentPlaneBounds = plane.getBounds();
            zidanPanel.updateZidans(currentTime);
            if (!zidanPanel.checkZidans(currentTime)) {
                lastDamageTime = currentTime;
            }
            if (rayPanel.updateRays(currentTime)) {
                rayPanel.repaint();
            }
            if (!rayPanel.checkCollisions(previousPlaneBounds, currentPlaneBounds, currentTime)) {
                lastDamageTime = currentTime;
            }
            if (plane.getLives() <= 0) {
                rayPanel.died();
                return;
            }

            if (currentTime - lastAddNewRayTime >= a) {
                addNewRay();
                addNewZidan();
                addTime = (addTime > a / 2 ? (int) Math.round(addTime * 0.99) : a / 2);
                lastAddNewRayTime = currentTime;
            }

            if (currentTime - lastFindPlaneTime >= addTime * 2L) {
                findPlane();
                lastFindPlaneTime = currentTime;
            }

            if (currentTime - lastScoreTime >= 100) {
                rayPanel.advanceScore();
                lastScoreTime = currentTime;
            }

            if (currentTime - helpTime >= 10000 && currentTime - lastDamageTime >= 300) {
                addHelp();
                helpTime = currentTime;
                helpLabel.justDoIt();
            }
            if (helpLabel.update(currentTime)) {
                updateLifeLabel();
            }
            if (currentTime - goldTime >= 10000) {
                addGold();
                goldTime = currentTime;
                goldLabel.justDoIt(user, this);
            }
            goldLabel.update(currentTime);

        });
        timer.start();
        addWindowListener(new WindowAdapter() {
            @Override
            public void windowClosing(WindowEvent e) {
                onWindowClosing();
            }
        });
    }

    public void updategoldLable() {
        if (mygoldLabel != null) {
            mygoldLabel.setText("当前金币：" + user.getGold());
        }
    }

    private void onWindowClosing() {
        user.save(user.getPassword());
        stopTimers(); // 停止所有计时器
    }

    private void addHelp() {
        helpLabel.setHelp();
        helpLabel.setVisible(true);
    }

    private void addGold() {
        goldLabel.setGold();
        goldLabel.setVisible(true);
    }

    // 增加一条射线的方法
    public void addNewRay() {
        if (rayPanel != null) {
            rayPanel.setAddNewRay();
        }
    }
    public void addNewZidan() {
        if (zidanPanel != null) {
            zidanPanel.setAddNewZidan();
        }
    }

    // 每 400ms 触发一次的方法，选择一个边并向飞机方向发出一条射线
    public void findPlane() {
        if (rayPanel != null) {
            rayPanel.findPlane();
        }
    }

    // 停止所有计时器的方法
    public void stopTimers() {
        if (timer != null) {
            timer.stop();
        }
    }

    private void updateMousePosition(JLayeredPane layeredPane) {
        if (!layeredPane.isShowing()) {
            return;
        }
        PointerInfo pointerInfo = MouseInfo.getPointerInfo();
        if (pointerInfo == null) {
            return;
        }
        Point point = pointerInfo.getLocation();
        SwingUtilities.convertPointFromScreen(point, layeredPane);
        if (layeredPane.contains(point)) {
            mouseX = point.x;
            mouseY = point.y;
        }
    }

    // 更新生命值标签的方法
    public void updateLifeLabel() {
        if (lifeLabel != null) {
            lifeLabel.setText("生命:" + plane.getLives());
        }
    }

    public void updateScoreLabel(int score) {
        if (scoreLabel != null) {
            scoreLabel.setText("当前分数：" + score);
        }
    }

    // 更新飞机位置的方法
    public void updatePlanePosition(int x, int y) {
        if (plane != null) {
            x = Math.max(0, Math.min(x, 1650));
            y = Math.max(0, Math.min(y, 1000));
            if (plane.getX() == x && plane.getY() == y) {
                return;
            }
            plane.setLocation(x, y);
        }
    }

    // 处理 ESC 键按下的方法
    private void onEscPressed() {
        plane.setLives(-1);
        rayPanel.died();
    }

    private static URL getResourceURL(String resourceName) {
        URL resourceURL = Main_play.class.getClassLoader().getResource(resourceName);
        if (resourceURL == null) {
            System.err.println("Resource not found: " + resourceName);
        }
        return resourceURL;
    }
}
